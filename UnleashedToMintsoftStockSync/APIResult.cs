using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;


namespace UnleashedToMintsoftStockSync
{
    [DataContract]
    class APIResult
    {
        [DataMember]
        public int ID { get; set; }

        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public String Message { get; set; }

        [DataMember]
        public String WarningMessage { get; set; }

    }
}
