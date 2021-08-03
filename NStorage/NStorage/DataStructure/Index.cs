using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage.DataStructure
{
    public class Index
    {
        // TODO need for pre-calculated properties, such as storage length, position to append, etc

        public List<IndexRecord> Records { get; set; } = new List<IndexRecord>();
    }
}
