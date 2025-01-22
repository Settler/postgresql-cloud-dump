using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace PgCloudDump.Service;

public class RestoreService(IOptions<RestoreOptions> options, ILogger<RestoreService> logger)
{
    private const string DatabaseComment = "Restored by PgCloudDump";

    private readonly IObjectStoreWriter _writer = ObjectStoreWriterFactory.Create(options.Value.ObjectStore, options.Value.Input);
    private readonly Regex _excludeRegex = new(options.Value.DatabaseExcludePattern, RegexOptions.Compiled);
    
    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        var backups = _writer.ListBackupsAsync();
        await foreach (var backupPath in backups)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var dir = Path.GetDirectoryName(backupPath);
            var serverToRestore = options.Value.Servers.FirstOrDefault(o=>o.InputFolder == dir);
            if (serverToRestore is null)
            {
                logger.LogInformation("Skipping {DatabaseBackupPath} because it folder doesn't exist in provided Servers list", backupPath);
                continue;
            }

            if (_excludeRegex.IsMatch(backupPath))
                continue;

            if (!Regex.IsMatch(backupPath, serverToRestore.DatabaseSelectPattern))
                continue;
            
            await RestoreBackupAsync(backupPath, serverToRestore, cancellationToken);
        }
    }

    private async Task RestoreBackupAsync(string backupPath, RestoreServer serverToRestore, CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(serverToRestore.ConnectionString);
        
        var database = Path.GetFileNameWithoutExtension(backupPath);
        await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (await DatabaseWasAlreadyRestoredAsync(connection, database, cancellationToken))
        {
            logger.LogInformation("Skipping restore {DatabaseBackupPath} to {Server} because it was already successfully restored.", backupPath, connectionString.Host);
            return;
        }
        
        logger.LogInformation("Starting restore {DatabaseBackupPath} to {Server}", backupPath, connectionString.Host);
        var sw = Stopwatch.StartNew();
        await CreateDatabaseIfNeedAsync(connection, database, cancellationToken);
        await using var backupStream = await _writer.GetBackupStreamAsync(backupPath);
        
        var pgRestoreCommand = $"-h {connectionString.Host} -p {connectionString.Port} -U {connectionString.Username} -d {database}";
        if (options.Value.Parallel?.JobsCount > 1)
        {
            logger.LogInformation("Downloading file to temp folder for {DatabaseBackupPath} and {Server}", backupPath, connectionString.Host);
            await using var tempFileStream = File.OpenWrite(options.Value.Parallel.TempDownloadedBackupFilePath);
            await backupStream.CopyToAsync(tempFileStream, cancellationToken);
            
            pgRestoreCommand += $" -j {options.Value.Parallel?.JobsCount} {options.Value.Parallel.TempDownloadedBackupFilePath}";
        }
        
        var processStartInfo = new ProcessStartInfo(options.Value.PathToPgRestore, pgRestoreCommand)
                               {
                                   RedirectStandardOutput = true,
                                   RedirectStandardError = true,
                                   RedirectStandardInput = true,
                                   UseShellExecute = false,
                                   CreateNoWindow = true
                               };
        processStartInfo.Environment.Add("PGPASSWORD", connectionString.Password);
        using var process = new Process();
        process.StartInfo = processStartInfo;
        process.OutputDataReceived += (_, args) => LogDataFromChildProcess(database, args.Data);
        process.ErrorDataReceived += (_, args) => LogDataFromChildProcess(database, args.Data);
        process.Start();
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (options.Value.Parallel is null || options.Value.Parallel.JobsCount == 1)
            await backupStream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);
        await MarkDatabaseAsRestoredAsync(connection, database, cancellationToken);

        if (options.Value.Parallel?.JobsCount > 1)
            File.Delete(options.Value.Parallel.TempDownloadedBackupFilePath);
        
        logger.LogInformation($"Restore of {{DatabaseBackupPath}} to {{Server}} was completed in {sw.Elapsed}", backupPath, connectionString.Host);
    }

    private void LogDataFromChildProcess(string database, string? logData)
    {
        logger.LogInformation($"[pg_restore]:{{Database}}: {logData}", database);
    }

    private async Task<bool> DatabaseWasAlreadyRestoredAsync(NpgsqlConnection connection, string database, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT pg_catalog.shobj_description(d.oid, 'pg_database') FROM pg_catalog.pg_database d WHERE datname = '{database}';", connection);
        var comment = await command.ExecuteScalarAsync(cancellationToken);
        return comment is string sComment && sComment.EndsWith(DatabaseComment);
    }
    
    private async Task MarkDatabaseAsRestoredAsync(NpgsqlConnection connection, string database, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"COMMENT ON DATABASE \"{database}\" IS '{DatabaseComment}';", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateDatabaseIfNeedAsync(NpgsqlConnection connection, string database, CancellationToken cancellationToken)
    {
        if (options.Value.ForceRecreate)
        {
            await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\"", connection);
            await dropCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        
        try
        {
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create database {Database}. If it already exists provide ForceRecreate option to drop and create new database.", database);
            throw;
        }
    }
}