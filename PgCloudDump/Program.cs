using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using Npgsql;

namespace PgCloudDump
{
    public class Program
    {
        private TimeSpan? _removeThreshold;

        [Option("-d | --database", "Database name to backup", CommandOptionType.SingleValue)]
        [Required]
        public string DbName { get; set; }

        [Option("-h | --host", "Specifies the host name of the machine on which the server is running", CommandOptionType.SingleValue)]
        [Required]
        public string Host { get; set; }

        [Option("-p | --port", "Specifies the TCP port or local Unix domain socket file extension on which the server is listening for connections",
            CommandOptionType.SingleValue)]
        public int Port { get; set; } = 5432;

        [Option("-U | --username", "User name to connect as", CommandOptionType.SingleValue)]
        [Required]
        public string UserName { get; set; }

        [Option("-W | --password", "Password", CommandOptionType.SingleValue)]
        [Required]
        public string Password { get; set; }

        [Option("-o | --output", "Only 'GoogleCloud' supported for now", CommandOptionType.SingleValue)]
        [Required]
        public ObjectStore Output { get; set; }

        [Option("-b | --bucket", "Name of bucket for GoogleCloud", CommandOptionType.SingleValue)]
        [Required]
        public string Bucket { get; set; }

        [Option("-r | --remove", "Remove backups older then this value. Example values: '60s', '20m', '12h', '1d'", CommandOptionType.SingleValue)]
        [Required]
        public string RemoveThresholdString { get; set; }

        [Option("--pg-dump-path", "Path to pg_dump executable", CommandOptionType.SingleValue)]
        public string PathToPgDump { get; set; } = "pg_dump";

        public TimeSpan RemoveThreshold
        {
            get
            {
                if (!_removeThreshold.HasValue)
                    _removeThreshold = ConvertToTimeSpan(RemoveThresholdString);

                return _removeThreshold.Value;
            }
        }

        private static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        public int OnExecute()
        {
            if (!CheckPgDump())
                return 1;

            CheckDbConnection();

            var writer = new ObjectStoreWriterFactory().Create(this);
            RemoveOldBackups(writer);
            var exitCode = CreateNewBackup(writer);

            return exitCode;
        }

        private bool CheckPgDump()
        {
            Console.WriteLine("Checking pg_dump existence...");
            
            var process = Process.Start(PathToPgDump, "--help");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"pg_dump not found on path '{PathToPgDump}'.");
                return false;
            }
            
            Console.WriteLine("pg_dump found");
            
            return true;
        }

        private void CheckDbConnection()
        {
            Console.WriteLine($"Testing connection to '{UserName}@{Host}:{Port}/{DbName}'...");

            var sqlConnection = new NpgsqlConnection($"Server={Host};Port={Port};Database={DbName};User Id={UserName};Password={Password};");
            using (sqlConnection)
                sqlConnection.Open();

            Console.WriteLine("Connection to DB established.");
        }

        private int CreateNewBackup(IObjectStoreWriter writer)
        {
            var processStartInfo = new ProcessStartInfo("bash",
                                                        $"-c \"{PathToPgDump} -h {Host} -p {Port} -U {UserName} -d {DbName} -F tar | gzip\"")
                                   {
                                       RedirectStandardOutput = true,
                                       UseShellExecute = false,
                                       CreateNoWindow = true
                                   };
            processStartInfo.Environment.Add("PGPASSWORD", Password);
            var process = new Process {StartInfo = processStartInfo};
            process.Start();

            var backupName = $"{DbName}_{DateTime.UtcNow:s}.tar.gz";
            Console.WriteLine($"Creating new backup: {backupName}...");

            writer.WriteAsync(backupName, process.StandardOutput.BaseStream).Wait();
            process.WaitForExit();

            Console.WriteLine("Creating new backup completed.");
            return process.ExitCode;
        }

        private void RemoveOldBackups(IObjectStoreWriter writer)
        {
            var now = DateTime.UtcNow;
            var removeOlderThen = now.Subtract(RemoveThreshold);

            Console.WriteLine("Removing old backups...");

            writer.DeleteOldBackupsAsync(removeOlderThen).Wait();

            Console.WriteLine("Removing old backups completed.");
        }

        public static TimeSpan ConvertToTimeSpan(string timeSpan)
        {
            if (timeSpan.Length < 2)
                throw new InvalidOperationException($"Invalid value for --remove option: '{timeSpan}'");

            var l = timeSpan.Length - 1;
            var value = timeSpan.Substring(0, l);
            var type = timeSpan.Substring(l, 1);

            switch (type)
            {
                case "d": return TimeSpan.FromDays(double.Parse(value));
                case "h": return TimeSpan.FromHours(double.Parse(value));
                case "m": return TimeSpan.FromMinutes(double.Parse(value));
                case "s": return TimeSpan.FromSeconds(double.Parse(value));
                case "f": return TimeSpan.FromMilliseconds(double.Parse(value));
                case "z": return TimeSpan.FromTicks(long.Parse(value));
                default: throw new InvalidOperationException($"Invalid value for --remove option: '{timeSpan}'");
            }
        }
    }
}