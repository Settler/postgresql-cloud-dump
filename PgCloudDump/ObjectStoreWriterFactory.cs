using System;

namespace PgCloudDump
{
    public class ObjectStoreWriterFactory
    {
        public IObjectStoreWriter Create(Program options)
        {
            switch (options.Output)
            {
                case ObjectStore.GoogleCloud:
                    return new GoogleCloudObjectStoreWriter(options.Bucket);
                case ObjectStore.HostPath:
                    return new HostPathObjectStoreWriter(options.HostPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(options.Output), options.Output, null);
            }   
        }
    }
}