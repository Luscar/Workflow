namespace SimpleBPM.Persistence;

public class OracleConfiguration
{
    public string ConnectionString { get; set; }
    public string TablePrefix { get; set; }

    public OracleConfiguration(string connectionString, string tablePrefix)
    {
        if (string.IsNullOrWhiteSpace(tablePrefix) || tablePrefix.Length < 3 || tablePrefix.Length > 10)
        {
            throw new ArgumentException("Table prefix must be between 3 and 10 characters", nameof(tablePrefix));
        }

        if (!tablePrefix.All(char.IsLetter))
        {
            throw new ArgumentException("Table prefix must contain only letters", nameof(tablePrefix));
        }

        ConnectionString = connectionString;
        TablePrefix = tablePrefix.ToUpper();
    }

    public string GetTableName(string baseName)
    {
        return $"{TablePrefix}_{baseName}";
    }
}
