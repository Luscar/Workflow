namespace SimpleBPM.Persistence;

public class OracleConfiguration
{
    public string ConnectionString { get; set; }
    public string TablePrefix { get; set; }

    public OracleConfiguration(string connectionString, string tablePrefix)
    {
        if (string.IsNullOrWhiteSpace(tablePrefix) || tablePrefix.Length < 3 || tablePrefix.Length > 10)
        {
            throw new ArgumentException("Le préfixe de table doit contenir entre 3 et 10 caractères", nameof(tablePrefix));
        }

        if (!tablePrefix.All(char.IsLetter))
        {
            throw new ArgumentException("Le préfixe de table ne doit contenir que des lettres", nameof(tablePrefix));
        }

        ConnectionString = connectionString;
        TablePrefix = tablePrefix.ToUpper();
    }

    public OracleConfiguration(string tablePrefix) : this("", tablePrefix)
    {
    }

    public string GetTableName(string baseName)
    {
        return $"{TablePrefix}_{baseName}";
    }
}
