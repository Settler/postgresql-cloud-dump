namespace PgCloudDump.Service;

public class BackupOptions
{
    public string CronExpression { get; set; }
    public string PathToPgDump { get; set; }
    public ObjectStore ObjectStore { get; set; }
    public string Output { get; set; }
    public BackupServer[] Servers { get; set; }
}

public class BackupServer
{
    public string ConnectionString { get; set; }
    public string DatabaseSelectPattern { get; set; }
}