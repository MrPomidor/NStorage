using System;

namespace NStorage.DataStructure
{
    internal class IndexRecord
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        /// <summary>
        /// We need this for Jil
        /// </summary>
        private IndexRecord() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public IndexRecord(DataReference dataReference, DataProperties properties)
        {
            DataReference = dataReference ?? throw new ArgumentNullException(nameof(dataReference));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public DataReference DataReference { get; set; }
        public DataProperties Properties { get; set; }
    }
}
