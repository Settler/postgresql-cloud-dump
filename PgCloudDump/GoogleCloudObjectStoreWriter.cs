using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace PgCloudDump
{
    public class GoogleCloudObjectStoreWriter : IObjectStoreWriter
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public GoogleCloudObjectStoreWriter(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName)) throw new ArgumentException("--bucket must be set when output is GoogleCloud", nameof(bucketName));

            _bucketName = bucketName;
            _storageClient = StorageClient.Create();
        }
        
        public Task WriteAsync(string path, Stream backupStream)
        {
            return _storageClient.UploadObjectAsync(_bucketName, path, null, backupStream);
        }

        public async Task DeleteOldBackupsAsync(string path, DateTime removeThreshold)
        {
            var objects = _storageClient.ListObjectsAsync(_bucketName, path);
            

            var objectsToDelete = new List<Object>();
            await objects.ForEachAsync(obj =>
                                       {
                                           if (obj.TimeCreated.Value.ToUniversalTime() <= removeThreshold)
                                               objectsToDelete.Add(obj);
                                       });

            if (objectsToDelete.Count == 0)
                Console.WriteLine("Nothing to delete.");
            
            foreach (var objToDelete in objectsToDelete)
            {
                Console.WriteLine($"Deleting {objToDelete.Name}, because it TimeCreated: {objToDelete.TimeCreated.Value.ToUniversalTime()} is older then threshold: {removeThreshold}");
                await _storageClient.DeleteObjectAsync(_bucketName, objToDelete.Name);
            }
        }
    }
}