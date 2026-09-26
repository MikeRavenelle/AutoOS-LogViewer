using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class SampleFiltersTests
{
    [Theory]
    [InlineData(FilterOp.Gt, 15f, 10, true)]
    [InlineData(FilterOp.Gt, 10f, 10, false)]
    [InlineData(FilterOp.Gte, 10f, 10, true)]
    [InlineData(FilterOp.Lt, 5f, 10, true)]
    [InlineData(FilterOp.Lte, 10f, 10, true)]
    [InlineData(FilterOp.Eq, 10f, 10, true)]
    [InlineData(FilterOp.Eq, 10f, 11, false)]
    [InlineData(FilterOp.Neq, 10f, 11, true)]
    [InlineData(FilterOp.Neq, 10f, 10, false)]
    public void SingleFilter_MatchesExpectedOp(FilterOp op, float value, double threshold, bool expected)
    {
        var filters = new[] { new SampleFilter([value], op, threshold) };
        Assert.Equal(expected, SampleFilters.Keep(filters, 0));
    }

    [Fact]
    public void MultipleFilters_CombineWithAnd()
    {
        var filters = new[]
        {
            new SampleFilter([95f], FilterOp.Gt, 90),
            new SampleFilter([3000f], FilterOp.Gt, 4000),
        };
        Assert.False(SampleFilters.Keep(filters, 0));

        var bothPass = new[]
        {
            new SampleFilter([95f], FilterOp.Gt, 90),
            new SampleFilter([5000f], FilterOp.Gt, 4000),
        };
        Assert.True(SampleFilters.Keep(bothPass, 0));
    }

    [Fact]
    public void NaN_NeverSatisfiesAnyOp()
    {
        var filters = new[] { new SampleFilter([float.NaN], FilterOp.Neq, 0) };
        Assert.False(SampleFilters.Keep(filters, 0));
    }
}
