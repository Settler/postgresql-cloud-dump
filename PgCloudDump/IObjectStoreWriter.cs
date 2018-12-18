using System;
using System.IO;
using System.Threading.Tasks;

namespace PgCloudDump
{
    public interface IObjectStoreWriter
    {
        Task WriteAsync(string fileName, Stream backupStream);
        Task DeleteOldBackupsAsync(DateTime removeThreshold);
    }
}