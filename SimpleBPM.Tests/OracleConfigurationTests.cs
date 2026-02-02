using SimpleBPM.Persistence;

namespace SimpleBPM.Tests;

public class OracleConfigurationTests
{
    [Fact]
    public void Constructor_ValidPrefix_Accepted()
    {
        var config = new OracleConfiguration("conn", "BPM");

        Assert.Equal("BPM", config.TablePrefix);
        Assert.Equal("conn", config.ConnectionString);
    }

    [Fact]
    public void Constructor_PrefixAutoUppercased()
    {
        var config = new OracleConfiguration("conn", "bpm");
        Assert.Equal("BPM", config.TablePrefix);
    }

    [Fact]
    public void Constructor_MixedCasePrefix_Uppercased()
    {
        var config = new OracleConfiguration("conn", "AbCdEf");
        Assert.Equal("ABCDEF", config.TablePrefix);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("A")]
    [InlineData("")]
    public void Constructor_PrefixTooShort_Throws(string prefix)
    {
        Assert.Throws<ArgumentException>(
            () => new OracleConfiguration("conn", prefix));
    }

    [Fact]
    public void Constructor_PrefixTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new OracleConfiguration("conn", "ABCDEFGHIJK"));
    }

    [Fact]
    public void Constructor_PrefixExactlyThreeChars_Accepted()
    {
        var config = new OracleConfiguration("conn", "ABC");
        Assert.Equal("ABC", config.TablePrefix);
    }

    [Fact]
    public void Constructor_PrefixExactlyTenChars_Accepted()
    {
        var config = new OracleConfiguration("conn", "ABCDEFGHIJ");
        Assert.Equal("ABCDEFGHIJ", config.TablePrefix);
    }

    [Theory]
    [InlineData("AB1")]
    [InlineData("A2C")]
    [InlineData("123")]
    public void Constructor_PrefixWithNumbers_Throws(string prefix)
    {
        Assert.Throws<ArgumentException>(
            () => new OracleConfiguration("conn", prefix));
    }

    [Theory]
    [InlineData("AB_C")]
    [InlineData("AB-C")]
    [InlineData("AB.C")]
    [InlineData("AB C")]
    public void Constructor_PrefixWithSpecialChars_Throws(string prefix)
    {
        Assert.Throws<ArgumentException>(
            () => new OracleConfiguration("conn", prefix));
    }

    [Fact]
    public void Constructor_NullPrefix_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new OracleConfiguration("conn", null!));
    }

    [Fact]
    public void GetTableName_ReturnsCorrectFormat()
    {
        var config = new OracleConfiguration("conn", "ABC");

        Assert.Equal("ABC_PROCESS_CONTEXT", config.GetTableName("PROCESS_CONTEXT"));
        Assert.Equal("ABC_HISTORIQUE", config.GetTableName("HISTORIQUE"));
    }
}
