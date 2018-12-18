using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace PgCloudDump
{
    public class GoogleCloudObjectStoreWriter : IObjectStoreWriter
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public GoogleCloudObjectStoreWriter(string bucketName)
        {
            _bucketName = bucketName;
            _storageClient = StorageClient.Create();
        }
        
        public Task WriteAsync(string fileName, Stream backupStream)
        {
            return _storageClient.UploadObjectAsync(_bucketName, fileName, null, backupStream);
        }
    }
}