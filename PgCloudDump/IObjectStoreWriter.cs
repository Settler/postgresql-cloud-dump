using System;
using System.IO;
using System.Threading.Tasks;

namespace PgCloudDump
{
    public interface IObjectStoreWriter
    {
        Task WriteAsync(string path, Stream backupStream);
        Task DeleteOldBackupsAsync(string path, DateTime removeThreshold);
    }
}