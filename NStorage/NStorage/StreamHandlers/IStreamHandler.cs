using System.IO;
using NStorage.DataStructure;

namespace NStorage.StreamHandlers
{
    /// <summary>
    /// Handles transforming input data for storage and transforming data after fetching from storage for consumption
    /// </summary>
    internal interface IStreamHandler
    {
        // TODO do async version later ???
        // TODO why we return bytes here ???
        (byte[] memory, DataProperties properties) PackData(Stream data, StreamInfo parameters);
        MemoryStream UnPackData(byte[] bytes, DataProperties dataProperties);
    }
}
