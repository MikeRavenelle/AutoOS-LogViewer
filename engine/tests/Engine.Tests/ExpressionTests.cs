using OpenLogViewer.Engine.Expressions;
using OpenLogViewer.Engine.Model;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class ExpressionTests
{
    private static ParsedLog MakeLog(params (string Name, string Unit, float[] Data)[] channels)
    {
        int n = channels[0].Data.Length;
        var time = new double[n];
        for (int i = 0; i < n; i++) time[i] = i * 0.1;
        return new ParsedLog
        {
            SourcePath = "mem",
            ParserId = "test",
            Metadata = new Dictionary<string, string>(),
            Time = time,
            Channels = [.. channels.Select((c, i) => new ChannelInfo(i, c.Name, c.Unit))],
            Data = [.. channels.Select(c => c.Data)],
        };
    }

    private static float[] Eval(string expression, ParsedLog log)
        => Evaluator.Evaluate(ExpressionParser.Parse(expression), log);

    [Fact]
    public void Precedence_MulBeforeAdd()
    {
        var log = MakeLog(("X", "", new float[] { 2 }));
        Assert.Equal(14f, Eval("2 + 3 * 4", log)[0]);
        Assert.Equal(20f, Eval("(2 + 3) * 4", log)[0]);
        Assert.Equal(-10f, Eval("-2 * 5", log)[0]);
    }

    [Fact]
    public void ChannelSubtraction_MatchesManualDelta()
    {
        var log = MakeLog(
            ("Boost Pressure Actual Sensor 1", "psi", new float[] { 17.2f, 30.5f }),
            ("Turbocharger Inlet Pressure", "psi", new float[] { 14.2f, 14.0f }));
        var result = Eval("[Boost Pressure Actual Sensor 1 (psi)] - [Turbocharger Inlet Pressure (psi)]", log);
        Assert.Equal(3.0f, result[0], 3);
        Assert.Equal(16.5f, result[1], 3);
    }

    [Fact]
    public void ChannelRef_ResolvesBareNameToo()
    {
        var log = MakeLog(("Engine Speed", "rpm", new float[] { 4000 }));
        Assert.Equal(4000f, Eval("[Engine Speed]", log)[0]);
    }

    [Fact]
    public void UnknownChannel_Throws()
    {
        var log = MakeLog(("X", "", new float[] { 1 }));
        var ex = Assert.Throws<ExpressionEvalException>(() => Eval("[Nope (psi)]", log));
        Assert.Contains("Nope", ex.Message);
    }

    [Fact]
    public void UnknownFunction_IsRejected()
    {
        var log = MakeLog(("X", "", new float[] { 1 }));
        Assert.Throws<ExpressionEvalException>(() => Eval("system(1)", log));
    }

    [Fact]
    public void MalformedExpressions_ThrowParseErrors()
    {
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("1 +"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("[Unclosed"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("(1"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("1 2"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("[X][abc]"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("[X]@abc"));
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("[X]@-0.5"));
    }

    [Fact]
    public void Functions_AbsMinMaxAvg()
    {
        var log = MakeLog(
            ("A", "", new float[] { -1, 4 }),
            ("B", "", new float[] { 2, 3 }));
        Assert.Equal([1f, 4f], Eval("abs([A])", log));
        Assert.Equal([-1f, 3f], Eval("min([A], [B])", log));
        Assert.Equal([2f, 4f], Eval("max([A], [B])", log));
        Assert.Equal([1.5f, 1.5f], Eval("avg([A])", log));
    }

    [Fact]
    public void Functions_TrigSqrtExpLog()
    {
        var log = MakeLog(("X", "", new float[] { 0 }));
        Assert.Equal(0f, Eval("sin([X])", log)[0], 5);
        Assert.Equal(1f, Eval("cos([X])", log)[0], 5);
        Assert.Equal(0f, Eval("tan([X])", log)[0], 5);
        Assert.Equal(3f, Eval("sqrt(9)", log)[0], 5);
        Assert.Equal(1f, Eval("exp([X])", log)[0], 5);
        Assert.Equal(1f, Eval("ln(exp(1))", log)[0], 3);
        Assert.Equal(2f, Eval("log(100)", log)[0], 5);
    }

    [Fact]
    public void Functions_FloorCeilRound()
    {
        var log = MakeLog(("X", "", new float[] { 2.5f }));
        Assert.Equal(2f, Eval("floor([X])", log)[0]);
        Assert.Equal(3f, Eval("ceil([X])", log)[0]);
        Assert.Equal(2f, Eval("round([X])", log)[0]);
    }

    [Fact]
    public void IndexShift_PreviousAndNextSample()
    {
        var log = MakeLog(("X", "", new float[] { 10, 20, 30, 40 }));
        Assert.Equal([float.NaN, 10f, 20f, 30f], Eval("[X][-1]", log));
        Assert.Equal([20f, 30f, 40f, float.NaN], Eval("[X][+1]", log));
    }

    [Fact]
    public void IndexShift_RateOfChangeMatchesDerivativeSign()
    {
        var log = MakeLog(("X", "", new float[] { 0, 10, 20, 30 }));
        var result = Eval("[X] - [X][-1]", log);
        Assert.True(float.IsNaN(result[0]));
        Assert.Equal(10f, result[1]);
        Assert.Equal(10f, result[2]);
        Assert.Equal(10f, result[3]);
    }

    [Fact]
    public void TimeShift_InterpolatesBetweenSamples()
    {
        var log = MakeLog(("X", "", new float[] { 0, 10, 20, 30, 40 }));
        var result = Eval("[X]@-0.05s", log);
        Assert.Equal(15f, result[2], 3);
    }

    [Fact]
    public void TimeShift_OutOfRange_IsNaN()
    {
        var log = MakeLog(("X", "", new float[] { 0, 10, 20 }));
        var result = Eval("[X]@-5s", log);
        Assert.True(float.IsNaN(result[0]));
    }

    [Fact]
    public void Shift_OnScalarOperand_IsNoOp()
    {
        var log = MakeLog(("X", "", new float[] { 1, 2 }));
        Assert.Equal([1.5f, 1.5f], Eval("avg([X])[-1]", log));
    }

    [Fact]
    public void Derivative_OfRampIsConstant()
    {
        var data = new float[50];
        for (int i = 0; i < data.Length; i++) data[i] = i * 1.0f;
        var log = MakeLog(("Ramp", "", data));
        var d = Eval("derivative([Ramp])", log);
        for (int i = 1; i < d.Length - 1; i++)
            Assert.Equal(10f, d[i], 2);
    }

    [Fact]
    public void Smooth_FlattensSpike()
    {
        var data = new float[41];
        data[20] = 10f;
        var log = MakeLog(("Spiky", "", data));
        var smoothed = Eval("smooth([Spiky], 1)", log);
        Assert.True(smoothed[20] < 2f, $"spike should be averaged down, got {smoothed[20]}");
        Assert.True(smoothed[20] > 0f);
    }

    [Fact]
    public void Kalman_DampensNoiseTowardTrueValue()
    {
        var data = new float[] { 100, 105, 95, 102, 98, 103, 97, 101, 99, 100 };
        var log = MakeLog(("Noisy", "", data));
        var filtered = Eval("kalman([Noisy], 0.01, 10)", log);

        Assert.Equal(100f, filtered[^1], 1);
        double rawSpread = data.Max() - data.Min();
        double filteredSpread = filtered.Max() - filtered.Min();
        Assert.True(filteredSpread < rawSpread, $"expected damping, got raw={rawSpread} filtered={filteredSpread}");
    }

    [Fact]
    public void Kalman_FirstSample_SeedsEstimateExactly()
    {
        var log = MakeLog(("X", "", new float[] { 42, 42, 42 }));
        var filtered = Eval("kalman([X], 0.1, 1)", log);
        Assert.Equal(42f, filtered[0], 3);
    }

    [Fact]
    public void Kalman_NaNSamples_CarryForwardWithoutResetting()
    {
        var data = new float[] { 50, float.NaN, float.NaN, 50 };
        var log = MakeLog(("X", "", data));
        var filtered = Eval("kalman([X], 0.1, 1)", log);

        Assert.Equal(50f, filtered[0], 2);
        Assert.Equal(50f, filtered[1], 2);
        Assert.Equal(50f, filtered[2], 2);
        Assert.Equal(50f, filtered[3], 2);
    }

    [Fact]
    public void Kalman_LeadingNaN_StaysNaNUntilFirstRealSample()
    {
        var data = new float[] { float.NaN, float.NaN, 10 };
        var log = MakeLog(("X", "", data));
        var filtered = Eval("kalman([X], 0.1, 1)", log);

        Assert.True(float.IsNaN(filtered[0]));
        Assert.True(float.IsNaN(filtered[1]));
        Assert.Equal(10f, filtered[2], 2);
    }

    [Fact]
    public void Kalman_RejectsNonPositiveNoiseParameters()
    {
        var log = MakeLog(("X", "", new float[] { 1, 2, 3 }));
        Assert.Throws<ExpressionEvalException>(() => Eval("kalman([X], 0, 1)", log));
        Assert.Throws<ExpressionEvalException>(() => Eval("kalman([X], 1, -1)", log));
    }

    [Fact]
    public void DivisionByZero_YieldsNaNNotCrash()
    {
        var log = MakeLog(("Z", "", new float[] { 0 }));
        Assert.True(float.IsNaN(Eval("1 / [Z]", log)[0]));
    }

    [Fact]
    public void GaugeBoostPreset_AvailableInAllFixtures()
    {
        var parser = new EcuConnectCsvParser();
        var dir = FixtureDir();
        foreach (var file in Directory.EnumerateFiles(dir, "*.csv"))
        {
            var log = parser.Parse(file);
            var gauge = MathPresets.All.Single(p => p.Id == "gauge-boost");
            Assert.True(MathPresets.IsAvailable(gauge, log), $"gauge-boost unavailable in {file}");

            var values = Eval(gauge.Expression, log);
            Assert.InRange(values[0], -5, 5);
        }
    }

    [Fact]
    public void ComputedChannel_BecomesFirstClass()
    {
        var log = MakeLog(("A", "psi", new float[] { 1, 2 }));
        var info = log.AddComputedChannel("Doubled", "psi", Eval("[A] * 2", log));

        Assert.True(info.Computed);
        Assert.Equal(1, info.Id);
        Assert.Equal([3f, 6f], Eval("[Doubled] + [A]", log));
    }

    private static string FixtureDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "fixtures");
    }
}
