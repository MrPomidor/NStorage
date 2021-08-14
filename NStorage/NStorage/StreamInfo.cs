﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage
{
    public class StreamInfo
    {
        public static readonly StreamInfo Empty = new StreamInfo();

        /// <summary>
        /// MD5 hash of the stream. Could be null. If value is
        /// specified, but actual hash of the data is different
        /// storage should throw ArgumentException
        /// </summary>
        //public byte[] Hash { get; set; }

        /// <summary>
        /// True if stream is compressed. Default false
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// The length of the stream. Can be null.
        /// If value is specified, but the actual length
        /// of the Stream is different the storage
        /// should throw ArgumentException
        /// </summary>
        //public long? Length { get; set; }

        // TODO is encrypted
    }
}
