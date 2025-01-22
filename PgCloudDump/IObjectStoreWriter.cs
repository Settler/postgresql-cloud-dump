using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PgCloudDump
{
    public interface IObjectStoreWriter
    {
        Task WriteAsync(string path, Stream backupStream);
        Task DeleteOldBackupsAsync(string path, DateTime removeThreshold);
        IAsyncEnumerable<string> ListBackupsAsync();
        Task<Stream> GetBackupStreamAsync(string path);
    }
}