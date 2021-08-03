using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage.DataStructure
{
    public class IndexRecord
    {
        public IndexRecord(string key, DataReference dataReference, DataProperties properties)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            DataReference = dataReference ?? throw new ArgumentNullException(nameof(dataReference));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public string Key { get; set; }
        public DataReference DataReference { get; set; }
        public DataProperties Properties { get; set; }
    }
}
