namespace OpenLogViewer.Engine.Series;

public enum FilterOp
{
    Gt,
    Gte,
    Lt,
    Lte,
    Eq,
    Neq,
}

public sealed record SampleFilter(float[] Channel, FilterOp Op, double Value);

public static class SampleFilters
{
    public static bool Keep(SampleFilter[] filters, int i)
    {
        foreach (var f in filters)
        {
            float v = f.Channel[i];
            if (float.IsNaN(v))
                return false;
            bool ok = f.Op switch
            {
                FilterOp.Gt => v > f.Value,
                FilterOp.Gte => v >= f.Value,
                FilterOp.Lt => v < f.Value,
                FilterOp.Lte => v <= f.Value,
                FilterOp.Eq => v == f.Value,
                FilterOp.Neq => v != f.Value,
                _ => false,
            };
            if (!ok)
                return false;
        }
        return true;
    }
}
