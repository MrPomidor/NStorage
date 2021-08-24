using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage
{
    public class StorageConfiguration
    {
        /// <summary>
        /// Maximum size in bytes of the storage file
        /// Zero means unlimited
        /// </summary>
        // TODO public long MaxStorageFile { get; set; }

        /// <summary>
        /// Maximum size in bytes of the index file
        /// Zero means unlimited
        /// </summary>
        // TODO public long MaxIndexFile { get; set; }

        /// <summary>
        /// Storage might compress data during persistence,
        /// if its size is greater than this value
        /// </summary>
        // TODO public long CompressionThreshold { get; set; }

        /// <summary>
        /// Folder where implementation should store Index and Storage File
        /// </summary>
        public string WorkingFolder { get; set; }

        // TODO docs
        // TODO helper method to set flush mode
        public FlushMode FlushMode { get; set; } = FlushMode.AtOnce;

        public byte[] AesEncryption_Key { get; set; }
    }

    public enum FlushMode
    {
        AtOnce,
        Deferred
        // TODO manual (flush only on manual)
    }
}
