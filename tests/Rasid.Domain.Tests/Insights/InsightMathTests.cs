using Rasid.Domain.Insights;

namespace Rasid.Domain.Tests.Insights;

public sealed class InsightMathTests
{
    [Theory]
    [InlineData(120, 100, 20.0)]
    [InlineData(80, 100, -20.0)]
    [InlineData(100, 100, 0.0)]
    public void PercentChange_compares_with_the_previous_period(int current, int previous, double expected)
    {
        Assert.Equal((decimal)expected, InsightMath.PercentChange(current, previous));
    }

    [Fact]
    public void PercentChange_is_unknown_without_a_previous_value()
    {
        Assert.Null(InsightMath.PercentChange(50, 0));
    }

    [Fact]
    public void Spikes_are_points_far_above_the_usual_level()
    {
        long[] hourly = [2, 3, 2, 4, 3, 2, 3, 21, 3, 2];

        var spikes = InsightMath.DetectSpikes(hourly);

        Assert.Equal([7], spikes.Select((isSpike, i) => (isSpike, i)).Where(x => x.isSpike).Select(x => x.i));
    }

    [Fact]
    public void Flat_or_short_series_have_no_spikes()
    {
        Assert.DoesNotContain(true, InsightMath.DetectSpikes([5, 5, 5, 5, 5, 5, 5]));
        Assert.DoesNotContain(true, InsightMath.DetectSpikes([1, 50]));
    }

    [Fact]
    public void Small_absolute_values_are_never_spikes()
    {
        Assert.DoesNotContain(true, InsightMath.DetectSpikes([0, 0, 0, 0, 0, 0, 0, 2], minimum: 3));
    }

    [Fact]
    public void Hashtags_are_extracted_once_each_including_arabic()
    {
        var tags = InsightMath.ExtractHashtags("Aid arrives #UAEAid #HumanitarianAid #uaeaid مساعدات #الإمارات email@x.com #a");

        Assert.Equal(["UAEAid", "HumanitarianAid", "الإمارات"], tags);
    }

    [Fact]
    public void Text_without_hashtags_yields_none()
    {
        Assert.Empty(InsightMath.ExtractHashtags("No tags here"));
        Assert.Empty(InsightMath.ExtractHashtags(null));
    }
}
