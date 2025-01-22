using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace PgCloudDump;

public class YandexCloudObjectStoreWriter : IObjectStoreWriter
{
    private readonly string _bucketName;
    private readonly AmazonS3Client _s3Client;
    private readonly TransferUtility _fileTransferUtility;

    public YandexCloudObjectStoreWriter(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName)) throw new ArgumentException("--bucket must be set when output is YandexCloud", nameof(bucketName));

        _bucketName = bucketName;
        var configsS3 = new AmazonS3Config
                        {
                            ServiceURL = "https://s3.yandexcloud.net"
                        };
        _s3Client = new AmazonS3Client(configsS3);
        _fileTransferUtility = new TransferUtility(_s3Client);
    }
        
    public async Task WriteAsync(string fileName, Stream backupStream)
    {
        await _fileTransferUtility.UploadAsync(backupStream, _bucketName, fileName);
    }

    public async Task DeleteOldBackupsAsync(string path, DateTime removeThreshold)
    {
        var listResponse = await _s3Client.ListObjectsAsync(_bucketName, path);
        var objectsToDelete = listResponse.S3Objects
                                          .Where(o => o.LastModified.ToUniversalTime() <= removeThreshold)
                                          .ToArray();
        if (objectsToDelete.Length == 0)
            Console.WriteLine("Nothing to delete.");
            
        foreach (var objToDelete in objectsToDelete)
        {
            Console.WriteLine($"Deleting {objToDelete.Key}, because it LastModified: {objToDelete.LastModified.ToUniversalTime()} is older then threshold: {removeThreshold}");
            await _s3Client.DeleteObjectAsync(_bucketName, objToDelete.Key);
        }
    }

    public async IAsyncEnumerable<string> ListBackupsAsync()
    {
        var listResponse = await _s3Client.ListObjectsAsync(_bucketName);
        foreach (var s3Object in listResponse.S3Objects)
        {
            yield return s3Object.Key;
        }
    }

    public Task<Stream> GetBackupStreamAsync(string path)
    {
        return _fileTransferUtility.OpenStreamAsync(_bucketName, path);
    }
}