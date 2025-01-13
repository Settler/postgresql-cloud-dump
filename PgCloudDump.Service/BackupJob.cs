using System.Diagnostics;
using System.Text.RegularExpressions;
using EasyCronJob.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace PgCloudDump.Service;

// ReSharper disable once ClassNeverInstantiated.Global
public class BackupJob(ICronConfiguration<BackupJob> cronConfiguration,
                       ILogger<BackupJob> logger,
                       IOptions<BackupOptions> options) : 
    CronJobService(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo, cronConfiguration.CronFormat)
{
    private readonly IObjectStoreWriter _writer = ObjectStoreWriterFactory.Create(options.Value.ObjectStore, options.Value.Output);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await CheckServersAsync(cancellationToken);
        await CheckPgDumpAsync(cancellationToken);
        
        await base.StartAsync(cancellationToken);
    }

    private async Task CheckPgDumpAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking pg_dump existence...");
            
        var process = Process.Start(options.Value.PathToPgDump, "-V");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            logger.LogError($"pg_dump not found on path '{options.Value.PathToPgDump}'.");
            throw new Exception($"pg_dump not found on path '{options.Value.PathToPgDump}'.");
        }
            
        logger.LogInformation("pg_dump found");
    }

    private async Task CheckServersAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking servers...");
        foreach (var server in options.Value.Servers)
        {
            var builder = new NpgsqlConnectionStringBuilder(server.ConnectionString);
            logger.LogInformation("Checking server {Server}...", builder.Host);

            var databaseCount = 0;
            var regex = new Regex(server.DatabaseSelectPattern);

            await using var npgsqlConnection = new NpgsqlConnection(server.ConnectionString);
            await npgsqlConnection.OpenAsync(cancellationToken);
            await using var command = npgsqlConnection.CreateCommand();
            command.CommandText = "SELECT datname FROM pg_database;";
            
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var database = reader.GetString(0);
                if (regex.IsMatch(database))
                    databaseCount++;
            }
            
            logger.LogInformation("Server {Server} has {DatabaseCount} databases available to backup by pattern '{DatabaseSelectPattern}'...",
                                  builder.Host, databaseCount, server.DatabaseSelectPattern);
        }
    }

    public override async Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting backup process...");
        foreach (var server in options.Value.Servers)
        {
            var builder = new NpgsqlConnectionStringBuilder(server.ConnectionString);

            try
            {
                logger.LogInformation("Backing up databases from server: {Server}...", builder.Host);

                var regex = new Regex(server.DatabaseSelectPattern);

                await using var npgsqlConnection = new NpgsqlConnection(server.ConnectionString);
                await npgsqlConnection.OpenAsync(cancellationToken);
                await using var command = npgsqlConnection.CreateCommand();
                command.CommandText = "SELECT datname FROM pg_database;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var database = reader.GetString(0);
                    if (regex.IsMatch(database))
                        await BackupDatabaseAsync(database, builder, cancellationToken);
                }
            
                logger.LogInformation("Finished backing up databases from server: {Server}", builder.Host);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to backup databases on server: {Server}", builder.Host);
            }
        }
    }

    private async Task BackupDatabaseAsync(string database, NpgsqlConnectionStringBuilder connectionString, CancellationToken cancellationToken)
    {
        try
        {
            var backupPath = $"{connectionString.Host}/{database}.backup";
            logger.LogInformation("Creating new backup of '{Database}' to '{DatabaseBackupPath}'...", database, backupPath);

            var sw = Stopwatch.StartNew();
            var pgDumpCommand = $"-h {connectionString.Host} -p {connectionString.Port} -U {connectionString.Username} -d {database} -Fc";
            var processStartInfo = new ProcessStartInfo(options.Value.PathToPgDump, pgDumpCommand)
                                   {
                                       RedirectStandardOutput = true,
                                       UseShellExecute = false,
                                       CreateNoWindow = true
                                   };
            processStartInfo.Environment.Add("PGPASSWORD", connectionString.Password);
            var process = new Process {StartInfo = processStartInfo};
            process.Start();

            await _writer.WriteAsync(backupPath, process.StandardOutput.BaseStream);
            await process.WaitForExitAsync(cancellationToken);
            
            process.Dispose();

            logger.LogInformation($"Creating new backup of '{{Database}}' completed in '{sw.Elapsed}'.", database);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to backup database '{Database}' on server '{Server}'.", database, connectionString.Host);
        }
    }
}