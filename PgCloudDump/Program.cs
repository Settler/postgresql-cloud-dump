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

        private static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        public int OnExecute()
        {
            var processStartInfo = new ProcessStartInfo("/Applications/Postgres.app/Contents/Versions/latest/bin/pg_dump",
                                                        $"-h {Host} -p {Port} -U {UserName} -d {DbName} -F tar")
                                   {
                                       RedirectStandardOutput = true,
                                       UseShellExecute = false,
                                       CreateNoWindow = true
                                   };
            processStartInfo.Environment.Add("PGPASSWORD", Password);
            var process = Process.Start(processStartInfo);

            using(var file = File.Open("test.tar.gz", FileMode.Create, FileAccess.Write))
            using (var gzip = new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true))
            {
                process.StandardOutput.BaseStream.CopyTo(gzip);
            }

            Console.WriteLine("Done");
            
            process.WaitForExit();
            
            return process.ExitCode;
        }
    }
}