using System;

namespace NStorage.DataStructure
{
    internal class IndexRecord
    {
        public IndexRecord(DataReference dataReference, DataProperties properties)
        {
            DataReference = dataReference ?? throw new ArgumentNullException(nameof(dataReference));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public DataReference DataReference { get; set; }
        public DataProperties Properties { get; set; }
    }
}
