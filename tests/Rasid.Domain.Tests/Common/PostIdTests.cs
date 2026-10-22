using Rasid.Domain.Common;

namespace Rasid.Domain.Tests.Common;

public sealed class PostIdTests
{
    [Theory]
    [InlineData("1850000000000000002", "1850000000000000001", true)]
    [InlineData("1850000000000000001", "1850000000000000002", false)]
    [InlineData("10000000000000000000", "9999999999999999999", true)] // longer id is newer
    [InlineData("1", null, true)]
    public void IsNewer_compares_snowflake_ids_numerically(string candidate, string? current, bool expected)
    {
        Assert.Equal(expected, PostId.IsNewer(candidate, current));
    }

    [Fact]
    public void Snowflake_time_is_decoded_from_x_ids()
    {
        // Documented example ID from the X API docs (posted on 6 Jan 2021).
        var created = PostId.SnowflakeTime("1346889436626259968");

        Assert.Equal(new DateOnly(2021, 1, 6), DateOnly.FromDateTime(created!.Value.UtcDateTime));
        Assert.Null(PostId.SnowflakeTime("149343200")); // sample IDs are not Snowflakes
    }

    [Fact]
    public void Newest_returns_the_largest_id()
    {
        Assert.Equal("300", PostId.Newest(["20", "300", "99"]));
        Assert.Null(PostId.Newest([]));
    }
}
