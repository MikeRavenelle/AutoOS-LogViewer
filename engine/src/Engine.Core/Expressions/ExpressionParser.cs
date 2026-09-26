using System.Globalization;

namespace OpenLogViewer.Engine.Expressions;

public abstract record Expr;
public sealed record NumberExpr(double Value) : Expr;
public sealed record ChannelExpr(string Reference) : Expr;
public sealed record UnaryExpr(Expr Operand) : Expr;
public sealed record BinaryExpr(char Op, Expr Left, Expr Right) : Expr;
public sealed record CallExpr(string Name, Expr[] Args) : Expr;
public sealed record IndexShiftExpr(Expr Operand, int Offset) : Expr;
public sealed record TimeShiftExpr(Expr Operand, double OffsetSeconds) : Expr;

public sealed class ExpressionParseException(string message, int position)
    : Exception($"{message} (at position {position})")
{
    public int Position { get; } = position;
}

public static class ExpressionParser
{
    public static Expr Parse(string input)
    {
        int pos = 0;
        var expr = ParseExpr(input, ref pos);
        SkipWs(input, ref pos);
        if (pos != input.Length)
            throw new ExpressionParseException($"Unexpected '{input[pos]}'", pos);
        return expr;
    }

    public static IReadOnlyList<string> ChannelReferences(Expr expr)
    {
        var refs = new List<string>();
        Walk(expr);
        return refs;

        void Walk(Expr e)
        {
            switch (e)
            {
                case ChannelExpr c: refs.Add(c.Reference); break;
                case UnaryExpr u: Walk(u.Operand); break;
                case BinaryExpr b: Walk(b.Left); Walk(b.Right); break;
                case CallExpr f: foreach (var a in f.Args) Walk(a); break;
                case IndexShiftExpr ix: Walk(ix.Operand); break;
                case TimeShiftExpr ts: Walk(ts.Operand); break;
            }
        }
    }

    private static Expr ParseExpr(string s, ref int pos)
    {
        var left = ParseTerm(s, ref pos);
        while (true)
        {
            SkipWs(s, ref pos);
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                char op = s[pos++];
                left = new BinaryExpr(op, left, ParseTerm(s, ref pos));
            }
            else return left;
        }
    }

    private static Expr ParseTerm(string s, ref int pos)
    {
        var left = ParseUnary(s, ref pos);
        while (true)
        {
            SkipWs(s, ref pos);
            if (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
            {
                char op = s[pos++];
                left = new BinaryExpr(op, left, ParseUnary(s, ref pos));
            }
            else return left;
        }
    }

    private static Expr ParseUnary(string s, ref int pos)
    {
        SkipWs(s, ref pos);
        if (pos < s.Length && s[pos] == '-')
        {
            pos++;
            return new UnaryExpr(ParseUnary(s, ref pos));
        }
        return ParsePrimary(s, ref pos);
    }

    private static Expr ParsePrimary(string s, ref int pos)
    {
        var atom = ParseAtom(s, ref pos);
        return ParseShiftSuffix(s, ref pos, atom);
    }

    private static Expr ParseAtom(string s, ref int pos)
    {
        SkipWs(s, ref pos);
        if (pos >= s.Length)
            throw new ExpressionParseException("Unexpected end of expression", pos);

        char c = s[pos];

        if (c == '(')
        {
            pos++;
            var inner = ParseExpr(s, ref pos);
            Expect(s, ref pos, ')');
            return inner;
        }

        if (c == '[')
        {
            int close = s.IndexOf(']', pos);
            if (close < 0)
                throw new ExpressionParseException("Unterminated channel reference '['", pos);
            string reference = s[(pos + 1)..close].Trim();
            if (reference.Length == 0)
                throw new ExpressionParseException("Empty channel reference", pos);
            pos = close + 1;
            return new ChannelExpr(reference);
        }

        if (char.IsDigit(c) || c == '.')
        {
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (!double.TryParse(s.AsSpan(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new ExpressionParseException($"Invalid number '{s[start..pos]}'", start);
            return new NumberExpr(value);
        }

        if (char.IsLetter(c))
        {
            int start = pos;
            while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_')) pos++;
            string name = s[start..pos];
            SkipWs(s, ref pos);
            Expect(s, ref pos, '(');
            var args = new List<Expr> { ParseExpr(s, ref pos) };
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == ',')
                {
                    pos++;
                    args.Add(ParseExpr(s, ref pos));
                }
                else break;
            }
            Expect(s, ref pos, ')');
            return new CallExpr(name.ToLowerInvariant(), [.. args]);
        }

        throw new ExpressionParseException($"Unexpected '{c}'", pos);
    }

    private static Expr ParseShiftSuffix(string s, ref int pos, Expr operand)
    {
        SkipWs(s, ref pos);
        if (pos < s.Length && s[pos] == '[')
        {
            int start = pos;
            pos++;
            SkipWs(s, ref pos);
            int sign = 1;
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                if (s[pos] == '-') sign = -1;
                pos++;
            }
            int digitsStart = pos;
            while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            if (pos == digitsStart)
                throw new ExpressionParseException("Expected an integer index shift, e.g. [-1]", start);
            int offset = sign * int.Parse(s.AsSpan(digitsStart, pos - digitsStart));
            Expect(s, ref pos, ']');
            return new IndexShiftExpr(operand, offset);
        }
        if (pos < s.Length && s[pos] == '@')
        {
            int start = pos;
            pos++;
            SkipWs(s, ref pos);
            int sign = 1;
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                if (s[pos] == '-') sign = -1;
                pos++;
            }
            int numStart = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (pos == numStart)
                throw new ExpressionParseException("Expected a time offset, e.g. @-0.5s", start);
            if (!double.TryParse(s.AsSpan(numStart, pos - numStart), NumberStyles.Float, CultureInfo.InvariantCulture, out var magnitude))
                throw new ExpressionParseException($"Invalid time offset '{s[numStart..pos]}'", numStart);
            if (pos >= s.Length || s[pos] != 's')
                throw new ExpressionParseException("Time offset must end in 's', e.g. @-0.5s", pos);
            pos++;
            return new TimeShiftExpr(operand, sign * magnitude);
        }
        return operand;
    }

    private static void Expect(string s, ref int pos, char c)
    {
        SkipWs(s, ref pos);
        if (pos >= s.Length || s[pos] != c)
            throw new ExpressionParseException($"Expected '{c}'", pos);
        pos++;
    }

    private static void SkipWs(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }
}
