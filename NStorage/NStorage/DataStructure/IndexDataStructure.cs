using System.Collections.Generic;

namespace NStorage.DataStructure
{
    internal class IndexDataStructure
    {
        // TODO need for pre-calculated properties, such as storage length, position to append, etc
        public Dictionary<string, IndexRecord> Records { get; set; } = new Dictionary<string, IndexRecord>();
    }
}
