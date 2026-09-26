using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Expressions;

public sealed record MathPreset(string Id, string Name, string Unit, string Expression);

public static class MathPresets
{
    public static readonly MathPreset[] All =
    [
        new("gauge-boost", "Gauge Boost", "psi",
            "[Boost Pressure Actual Sensor 1 (psi)] - [Turbocharger Inlet Pressure (psi)]"),
        new("gauge-boost-target", "Boost Target Gauge", "psi",
            "[Boost Pressure Target Sensor 1 (psi)] - [Turbocharger Inlet Pressure (psi)]"),
        new("lambda-delta", "Lambda Delta", "lambda",
            "[Lambda Actual B1S1 (lambda)] - [Lambda Target (lambda)]"),
        new("boost-error", "Boost Error", "psi",
            "[Boost Error (RaceROM) (psi)]"),
    ];

    public static bool IsAvailable(MathPreset preset, ParsedLog log)
    {
        var expr = ExpressionParser.Parse(preset.Expression);
        foreach (var reference in ExpressionParser.ChannelReferences(expr))
        {
            if (Evaluator.Resolve(log, reference) is null)
                return false;
        }
        return true;
    }
}
