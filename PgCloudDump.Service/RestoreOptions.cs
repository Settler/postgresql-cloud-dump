namespace PgCloudDump.Service;

public class RestoreOptions
{
    public required ObjectStore ObjectStore { get; set; }
    public required string Input { get; set; }
    public required string PathToPgRestore { get; set; }
    public required string DatabaseExcludePattern { get; set; }
    public ParallelRestoreOptions? Parallel { get; set; }
    public required bool ForceRecreate { get; set; }
    public required RestoreServer[] Servers { get; set; }
}

public class ParallelRestoreOptions
{
    public required int JobsCount { get; set; }
    public required string TempDownloadedBackupFilePath { get; set; }
}

public class RestoreServer
{
    public required string InputFolder { get; set; }
    public required string ConnectionString { get; set; }
    public required string DatabaseSelectPattern { get; set; }
}