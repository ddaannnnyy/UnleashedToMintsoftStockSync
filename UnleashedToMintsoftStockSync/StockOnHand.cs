using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace UnleashedToMintsoftStockSync
{
    [DataContract]
    class StockOnHand
    {
        [DataMember]
        public string SKU { get; set; }

        [DataMember]
        public int Quantity { get; set; }

        [DataMember]
        public int WarehouseId { get; set; }
    }
}
