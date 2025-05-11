namespace PgCloudDump.Service;

public class BackupOptions
{
    public required string CronExpression { get; set; }
    public required string PathToPgDump { get; set; }
    public required ObjectStore ObjectStore { get; set; }
    public required string Output { get; set; }
    public required int JobsCount { get; set; }
    public required BackupServer[] Servers { get; set; }
}

public class BackupServer
{
    public required string ConnectionString { get; set; }
    public required string DatabaseSelectPattern { get; set; }
    public string? DatabaseExcludePattern { get; set; }
}