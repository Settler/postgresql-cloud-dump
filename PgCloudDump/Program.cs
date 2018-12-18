using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;

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
            var writer = new ObjectStoreWriterFactory().Create(this);
            var now = DateTime.UtcNow;
            var removeOlderThen = now.Subtract(RemoveThreshold);
            
            Console.WriteLine("Removing old backups...");
            
            writer.DeleteOldBackupsAsync(removeOlderThen).Wait();
            
            Console.WriteLine("Removing old backups completed.");
            
            var processStartInfo = new ProcessStartInfo("bash",
                                                        $"-c \"/Applications/Postgres.app/Contents/Versions/latest/bin/pg_dump -h {Host} -p {Port} -U {UserName} -d {DbName} -F tar | gzip\"")
                                   {
                                       RedirectStandardOutput = true,
                                       UseShellExecute = false,
                                       CreateNoWindow = true
                                   };
            processStartInfo.Environment.Add("PGPASSWORD", Password);
            var process = new Process {StartInfo = processStartInfo};

            var backupName = $"{DbName}_{now:s}.tar.gz";
            Console.WriteLine($"Creating new backup: {backupName}...");
            
            process.Start();
            writer.WriteAsync(backupName, process.StandardOutput.BaseStream).Wait();
            process.WaitForExit();
            
            Console.WriteLine("Creating new backup completed.");
            
            return process.ExitCode;
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