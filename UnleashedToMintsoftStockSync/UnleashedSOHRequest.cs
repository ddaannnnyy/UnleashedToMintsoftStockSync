using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace UnleashedToMintsoftStockSync
{
    class UnleashedSOHRequest
    {
        public class Rootobject
        {
            public Class1[] Property1 { get; set; }
        }

        public class Class1
        {
            public Pagination Pagination { get; set; }
            public Item[] Items { get; set; }
        }

        public class Pagination
        {
            public int NumberOfItems { get; set; }
            public int PageSize { get; set; }
            public int PageNumber { get; set; }
            public int NumberOfPages { get; set; }
        }

        public class Item
        {
            public string ProductCode { get; set; }
            public string ProductDescription { get; set; }
            public string ProductGuid { get; set; }
            public string Guid { get; set; }
        }
    }
}
