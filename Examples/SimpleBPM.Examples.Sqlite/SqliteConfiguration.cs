namespace SimpleBPM.Examples.Sqlite;

public class SqliteConfiguration
{
    public string ConnectionString { get; }
    public string TablePrefix { get; }

    public SqliteConfiguration(string databasePath, string tablePrefix)
    {
        if (string.IsNullOrWhiteSpace(tablePrefix) || tablePrefix.Length < 3 || tablePrefix.Length > 10)
            throw new ArgumentException("Table prefix must be between 3 and 10 characters", nameof(tablePrefix));

        if (!tablePrefix.All(char.IsLetter))
            throw new ArgumentException("Table prefix must contain only letters", nameof(tablePrefix));

        ConnectionString = $"Data Source={databasePath}";
        TablePrefix = tablePrefix.ToUpper();
    }

    public string GetTableName(string baseName) => $"{TablePrefix}_{baseName}";
}
