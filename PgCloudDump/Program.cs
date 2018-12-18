using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using McMaster.Extensions.CommandLineUtils;

namespace PgCloudDump
{
    public class Program
    {
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
        public string Bucket { get; set; }

        private static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        public int OnExecute()
        {
            var writer = new ObjectStoreWriterFactory().Create(this);
            
            var processStartInfo = new ProcessStartInfo("bash",
                                                        $"-c \"/Applications/Postgres.app/Contents/Versions/latest/bin/pg_dump -h {Host} -p {Port} -U {UserName} -d {DbName} -F tar | gzip\"")
                                   {
                                       RedirectStandardOutput = true,
                                       UseShellExecute = false,
                                       CreateNoWindow = true
                                   };
            processStartInfo.Environment.Add("PGPASSWORD", Password);
            var process = new Process {StartInfo = processStartInfo};
            process.Start();
            
            writer.WriteAsync($"{DbName}.tar.gz", process.StandardOutput.BaseStream).Wait();

            process.WaitForExit();
            
            return process.ExitCode;
        }
    }
}