using SimpleBPM.Persistence;

namespace SimpleBPM.Tests;

public class OracleConfigurationTests
{
    [Fact]
    public void Constructor_ValidPrefix_CreatesConfiguration()
    {
        var config = new OracleConfiguration("BPM");

        Assert.Equal("BPM", config.TablePrefix);
    }

    [Fact]
    public void Constructor_ConvertsToUpperCase()
    {
        var config = new OracleConfiguration("bpm");

        Assert.Equal("BPM", config.TablePrefix);
    }

    [Fact]
    public void Constructor_PrefixTooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("AB"));
    }

    [Fact]
    public void Constructor_PrefixTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("ABCDEFGHIJK"));
    }

    [Fact]
    public void Constructor_PrefixWithNumbers_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration("BPM123"));
    }

    [Fact]
    public void Constructor_EmptyPrefix_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OracleConfiguration(""));
    }

    [Fact]
    public void GetTableName_ReturnsCorrectFormat()
    {
        var config = new OracleConfiguration("BPM");

        Assert.Equal("BPM_PROCESS_CONTEXT", config.GetTableName("PROCESS_CONTEXT"));
        Assert.Equal("BPM_HISTORIQUE", config.GetTableName("HISTORIQUE"));
    }
}
