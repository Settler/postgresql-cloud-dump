using System;

namespace PgCloudDump
{
    public abstract class ObjectStoreWriterFactory
    {
        public static IObjectStoreWriter Create(ObjectStore objectStore, string output)
        {
            return objectStore switch
            {
                ObjectStore.GoogleCloud => new GoogleCloudObjectStoreWriter(output),
                ObjectStore.HostPath => new HostPathObjectStoreWriter(output),
                ObjectStore.YandexCloud => new YandexCloudObjectStoreWriter(output),
                _ => throw new ArgumentOutOfRangeException(nameof(objectStore), objectStore, null)
            };
        }
    }
}