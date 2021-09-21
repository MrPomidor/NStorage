using System.Collections.Generic;

namespace NStorage.DataStructure
{
    public class Index
    {
        // TODO need for pre-calculated properties, such as storage length, position to append, etc

        public List<IndexRecord> Records { get; set; } = new List<IndexRecord>();
    }
}
