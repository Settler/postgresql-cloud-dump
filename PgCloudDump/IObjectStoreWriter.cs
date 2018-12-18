using System.IO;
using System.Threading.Tasks;

namespace PgCloudDump
{
    public interface IObjectStoreWriter
    {
        Task WriteAsync(string fileName, Stream backupStream);
    }
}