using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace PgCloudDump
{
    public class HostPathObjectStoreWriter : IObjectStoreWriter
    {
        private readonly string _hostPath;

        public HostPathObjectStoreWriter(string hostPath)
        {
            if (string.IsNullOrWhiteSpace(hostPath)) throw new ArgumentException("--path must be set when output is HostPath", nameof(hostPath));
            
            _hostPath = hostPath;
        }
        
        public async Task WriteAsync(string path, Stream backupStream)
        {
            if (!Directory.Exists(_hostPath))
                Directory.CreateDirectory(_hostPath);

            var fullOutputPath = Path.Combine(_hostPath, path);
            
            var memory = new Memory<byte>(new byte[1024]);
            using (var fileStream = File.OpenWrite(fullOutputPath))
            {
                while (await backupStream.ReadAsync(memory) > 0)
                {
                    await fileStream.WriteAsync(memory);
                }
            }
        }

        public Task DeleteOldBackupsAsync(string path, DateTime removeThreshold)
        {
            if (!Directory.Exists(_hostPath))
            {
                Console.WriteLine($"Host path '{_hostPath}' doesn't exist, so there is no old backups.");
                return Task.CompletedTask;
            }

            var fullOutputPath = Path.Combine(_hostPath, path);
            var files = Directory.GetFiles(fullOutputPath, "*.tar.gz");
            var objectsToDelete = files.Where(o => File.GetCreationTimeUtc(o) <= removeThreshold).ToArray();

            if (objectsToDelete.Length == 0)
            {
                Console.WriteLine("Nothing to delete.");
                return Task.CompletedTask;
            }
            
            foreach (var objToDelete in objectsToDelete)
            {
                var timeCreated = File.GetCreationTimeUtc(objToDelete);
                Console.WriteLine($"Deleting {Path.GetFileName(objToDelete)}, because it TimeCreated: {timeCreated} is older then threshold: {removeThreshold}");
                File.Delete(objToDelete);
            }

            return Task.CompletedTask;
        }
    }
}