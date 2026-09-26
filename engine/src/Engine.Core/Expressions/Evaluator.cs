using OpenLogViewer.Engine.Model;
using OpenLogViewer.Engine.Series;

namespace OpenLogViewer.Engine.Expressions;

public sealed class ExpressionEvalException(string message) : Exception(message);

public static class Evaluator
{
    private static readonly Dictionary<string, Func<float, float>> UnaryFunctions = new()
    {
        ["abs"] = MathF.Abs,
        ["sin"] = MathF.Sin,
        ["cos"] = MathF.Cos,
        ["tan"] = MathF.Tan,
        ["sqrt"] = MathF.Sqrt,
        ["exp"] = MathF.Exp,
        ["ln"] = MathF.Log,
        ["log"] = MathF.Log10,
        ["floor"] = MathF.Floor,
        ["ceil"] = MathF.Ceiling,
        ["round"] = MathF.Round,
    };

    public static float[] Evaluate(Expr expr, ParsedLog log)
    {
        var value = Eval(expr, log);
        return value.IsScalar ? Broadcast(value.Scalar, log.SampleCount) : value.Array!;
    }

    public static ChannelInfo? Resolve(ParsedLog log, string reference)
    {
        foreach (var c in log.Channels)
        {
            string label = c.Unit.Length > 0 ? $"{c.Name} ({c.Unit})" : c.Name;
            if (label == reference) return c;
        }
        foreach (var c in log.Channels)
        {
            if (c.Name == reference) return c;
        }
        return null;
    }

    private readonly record struct Value(float[]? Array, float Scalar)
    {
        public bool IsScalar => Array is null;
    }

    private static Value Eval(Expr expr, ParsedLog log)
    {
        switch (expr)
        {
            case NumberExpr n:
                return new Value(null, (float)n.Value);

            case ChannelExpr c:
            {
                var channel = Resolve(log, c.Reference)
                    ?? throw new ExpressionEvalException($"Unknown channel [{c.Reference}]");
                return new Value(log.Data[channel.Id], 0);
            }

            case UnaryExpr u:
            {
                var v = Eval(u.Operand, log);
                return v.IsScalar
                    ? new Value(null, -v.Scalar)
                    : new Value(Map(v.Array!, x => -x), 0);
            }

            case BinaryExpr b:
            {
                var l = Eval(b.Left, log);
                var r = Eval(b.Right, log);
                Func<float, float, float> op = b.Op switch
                {
                    '+' => static (x, y) => x + y,
                    '-' => static (x, y) => x - y,
                    '*' => static (x, y) => x * y,
                    '/' => static (x, y) => y == 0 ? float.NaN : x / y,
                    _ => throw new ExpressionEvalException($"Unknown operator '{b.Op}'"),
                };
                return Zip(l, r, op);
            }

            case CallExpr f:
                return EvalCall(f, log);

            case IndexShiftExpr ix:
            {
                var v = Eval(ix.Operand, log);
                return v.IsScalar ? v : new Value(IndexShift(v.Array!, ix.Offset), 0);
            }

            case TimeShiftExpr ts:
            {
                var v = Eval(ts.Operand, log);
                return v.IsScalar ? v : new Value(TimeShift(v.Array!, log.Time, ts.OffsetSeconds), 0);
            }

            default:
                throw new ExpressionEvalException($"Unknown expression node {expr.GetType().Name}");
        }
    }

    private static Value EvalCall(CallExpr f, ParsedLog log)
    {
        if (UnaryFunctions.TryGetValue(f.Name, out var fn))
        {
            RequireArgs(f, 1);
            var v = Eval(f.Args[0], log);
            return v.IsScalar ? new Value(null, fn(v.Scalar)) : new Value(Map(v.Array!, fn), 0);
        }

        switch (f.Name)
        {
            case "min":
            {
                RequireArgs(f, 2);
                return Zip(Eval(f.Args[0], log), Eval(f.Args[1], log), MathF.Min);
            }
            case "max":
            {
                RequireArgs(f, 2);
                return Zip(Eval(f.Args[0], log), Eval(f.Args[1], log), MathF.Max);
            }
            case "avg":
            {
                RequireArgs(f, 1);
                var v = Eval(f.Args[0], log);
                if (v.IsScalar) return v;
                double sum = 0;
                int n = 0;
                foreach (var x in v.Array!)
                {
                    if (float.IsNaN(x)) continue;
                    sum += x;
                    n++;
                }
                return new Value(null, n == 0 ? float.NaN : (float)(sum / n));
            }
            case "derivative":
            {
                RequireArgs(f, 1);
                var v = Eval(f.Args[0], log);
                if (v.IsScalar) return new Value(null, 0);
                return new Value(Derivative(v.Array!, log.Time), 0);
            }
            case "smooth":
            {
                RequireArgs(f, 2);
                var v = Eval(f.Args[0], log);
                var window = Eval(f.Args[1], log);
                if (!window.IsScalar || window.Scalar <= 0)
                    throw new ExpressionEvalException("smooth(x, seconds) needs a positive constant window");
                if (v.IsScalar) return v;
                return new Value(Smooth(v.Array!, log.Time, window.Scalar), 0);
            }
            case "kalman":
            {
                RequireArgs(f, 3);
                var v = Eval(f.Args[0], log);
                var q = Eval(f.Args[1], log);
                var r = Eval(f.Args[2], log);
                if (!q.IsScalar || q.Scalar <= 0 || !r.IsScalar || r.Scalar <= 0)
                    throw new ExpressionEvalException("kalman(x, q, r) needs positive constant process/measurement noise");
                if (v.IsScalar) return v;
                return new Value(Kalman(v.Array!, q.Scalar, r.Scalar), 0);
            }
            default:
                throw new ExpressionEvalException(
                    $"Unknown function '{f.Name}'. Allowed: {string.Join(", ", UnaryFunctions.Keys)}, min, max, avg, derivative, smooth, kalman.");
        }
    }

    private static void RequireArgs(CallExpr f, int count)
    {
        if (f.Args.Length != count)
            throw new ExpressionEvalException($"{f.Name}() takes {count} argument(s), got {f.Args.Length}");
    }

    private static float[] Broadcast(float scalar, int n)
    {
        var result = new float[n];
        System.Array.Fill(result, scalar);
        return result;
    }

    private static float[] IndexShift(float[] v, int offset)
    {
        int n = v.Length;
        var result = new float[n];
        for (int i = 0; i < n; i++)
        {
            int src = i + offset;
            result[i] = src >= 0 && src < n ? v[src] : float.NaN;
        }
        return result;
    }

    private static float[] TimeShift(float[] v, double[] t, double offsetSeconds)
    {
        int n = v.Length;
        var result = new float[n];
        for (int i = 0; i < n; i++)
        {
            double target = t[i] + offsetSeconds;
            if (n == 0 || target < t[0] || target > t[^1]) { result[i] = float.NaN; continue; }

            int hi = TimeIndex.LowerBound(t, target);
            int lo = hi > 0 ? hi - 1 : hi;
            if (lo == hi || t[hi] == target)
            {
                result[i] = v[hi];
                continue;
            }
            double frac = (target - t[lo]) / (t[hi] - t[lo]);
            float a = v[lo], b = v[hi];
            result[i] = float.IsNaN(a) || float.IsNaN(b) ? float.NaN : (float)(a + (b - a) * frac);
        }
        return result;
    }

    private static float[] Map(float[] source, Func<float, float> fn)
    {
        var result = new float[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = fn(source[i]);
        return result;
    }

    private static Value Zip(Value l, Value r, Func<float, float, float> op)
    {
        if (l.IsScalar && r.IsScalar)
            return new Value(null, op(l.Scalar, r.Scalar));

        var length = (l.Array ?? r.Array)!.Length;
        if (l.Array is not null && r.Array is not null && l.Array.Length != r.Array.Length)
            throw new ExpressionEvalException("Channel lengths differ");

        var result = new float[length];
        for (int i = 0; i < length; i++)
        {
            float x = l.IsScalar ? l.Scalar : l.Array![i];
            float y = r.IsScalar ? r.Scalar : r.Array![i];
            result[i] = op(x, y);
        }
        return new Value(result, 0);
    }

    private static float[] Derivative(float[] v, double[] t)
    {
        int n = v.Length;
        var result = new float[n];
        if (n < 2) return result;
        for (int i = 0; i < n; i++)
        {
            int lo = Math.Max(0, i - 1);
            int hi = Math.Min(n - 1, i + 1);
            double dt = t[hi] - t[lo];
            result[i] = dt == 0 ? float.NaN : (float)((v[hi] - v[lo]) / dt);
        }
        return result;
    }

    private static float[] Smooth(float[] v, double[] t, float windowSeconds)
    {
        int n = v.Length;
        var result = new float[n];
        double half = windowSeconds / 2.0;
        int lo = 0, hi = 0;
        double sum = 0;
        int count = 0;

        for (int i = 0; i < n; i++)
        {
            while (hi < n && t[hi] <= t[i] + half)
            {
                if (!float.IsNaN(v[hi])) { sum += v[hi]; count++; }
                hi++;
            }
            while (lo < n && t[lo] < t[i] - half)
            {
                if (!float.IsNaN(v[lo])) { sum -= v[lo]; count--; }
                lo++;
            }
            result[i] = count == 0 ? float.NaN : (float)(sum / count);
        }
        return result;
    }

    private static float[] Kalman(float[] v, float q, float r)
    {
        int n = v.Length;
        var result = new float[n];
        bool initialized = false;
        double x = 0, p = 1;

        for (int i = 0; i < n; i++)
        {
            float z = v[i];
            if (!initialized)
            {
                if (float.IsNaN(z)) { result[i] = float.NaN; continue; }
                x = z;
                p = 1;
                initialized = true;
                result[i] = (float)x;
                continue;
            }

            double pPredicted = p + q;
            if (float.IsNaN(z))
            {
                p = pPredicted;
            }
            else
            {
                double gain = pPredicted / (pPredicted + r);
                x += gain * (z - x);
                p = (1 - gain) * pPredicted;
            }
            result[i] = (float)x;
        }
        return result;
    }
}
