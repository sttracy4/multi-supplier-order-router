using OrderRouter.Api.Utilities;
using Xunit;

namespace OrderRouter.Api.Tests.Unit;

public class ZipRangeParserTests
{
    [Fact]
    public void Expand_ExplicitList_ReturnsEachZip()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("11410, 11419, 11438");
        Assert.False(nationwide);
        Assert.Equal(new[] { "11410", "11419", "11438" }, zips);
    }

    [Fact]
    public void Expand_SimpleRange_ReturnsAllZips()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("10001-10005");
        Assert.False(nationwide);
        Assert.Equal(new[] { "10001", "10002", "10003", "10004", "10005" }, zips);
    }

    [Fact]
    public void Expand_RangeWithoutLeadingZeros_PadsCorrectly()
    {
        // "2164-2167" should produce zero-padded 5-char ZIPs
        var (zips, nationwide) = ZipRangeParser.Expand("2164-2167");
        Assert.False(nationwide);
        Assert.Equal(new[] { "02164", "02165", "02166", "02167" }, zips);
    }

    [Fact]
    public void Expand_NationwideRange_SetsFlag()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("00100-99999");
        Assert.True(nationwide);
        Assert.Empty(zips);
    }

    [Fact]
    public void Expand_MixedTokens_ReturnsCorrectZips()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("10001, 10003-10005, 10009");
        Assert.False(nationwide);
        Assert.Equal(new[] { "10001", "10003", "10004", "10005", "10009" }, zips);
    }

    [Fact]
    public void Expand_SingleZip_ReturnsOne()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("10001");
        Assert.False(nationwide);
        Assert.Equal(new[] { "10001" }, zips);
    }

    [Fact]
    public void NormalizeZip_ShortZip_PadsToFiveChars()
    {
        Assert.Equal("02130", ZipRangeParser.NormalizeZip("2130"));
    }

    [Fact]
    public void NormalizeZip_AlreadyFiveChars_Unchanged()
    {
        Assert.Equal("10001", ZipRangeParser.NormalizeZip("10001"));
    }

    [Fact]
    public void Expand_NonNumericTokens_AreSkipped()
    {
        var (zips, nationwide) = ZipRangeParser.Expand("10001, N/A, 10003, abc");
        Assert.False(nationwide);
        Assert.Equal(new[] { "10001", "10003" }, zips);
    }

    [Fact]
    public void Expand_EmptyString_ReturnsEmpty()
    {
        var (zips, nationwide) = ZipRangeParser.Expand(string.Empty);
        Assert.False(nationwide);
        Assert.Empty(zips);
    }
}
