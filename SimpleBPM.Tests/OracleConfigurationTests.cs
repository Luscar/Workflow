using SimpleBPM.Persistence;

namespace SimpleBPM.Tests;

public class OracleConfigurationTests
{
    [Fact]
    public void Constructor_ValidPrefix_CreatesConfiguration()
    {
        var config = new OracleConfiguration("connString", "BPM");

        Assert.Equal("connString", config.ConnectionString);
        Assert.Equal("BPM", config.TablePrefix);
    }

    [Fact]
    public void Constructor_ConvertsToUpperCase()
    {
        var config = new OracleConfiguration("conn", "bpm");

        Assert.Equal("BPM", config.TablePrefix);
    }

    [Fact]
    public void Constructor_PrefixTooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("conn", "AB"));
    }

    [Fact]
    public void Constructor_PrefixTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("conn", "ABCDEFGHIJK"));
    }

    [Fact]
    public void Constructor_PrefixWithNumbers_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("conn", "BPM123"));
    }

    [Fact]
    public void Constructor_EmptyPrefix_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("conn", ""));
    }

    [Fact]
    public void Constructor_WithPrefixOnly()
    {
        var config = new OracleConfiguration("TEST");

        Assert.Equal("", config.ConnectionString);
        Assert.Equal("TEST", config.TablePrefix);
    }

    [Fact]
    public void GetTableName_ReturnsCorrectFormat()
    {
        var config = new OracleConfiguration("conn", "BPM");

        Assert.Equal("BPM_PROCESS_CONTEXT", config.GetTableName("PROCESS_CONTEXT"));
        Assert.Equal("BPM_HISTORIQUE", config.GetTableName("HISTORIQUE"));
    }
}
