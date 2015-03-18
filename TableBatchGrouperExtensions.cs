using AzureStorage.TableBatchGrouper;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Microsoft.WindowsAzure.Storage
{
    public static class TableBatchGrouperExtensions
    {
        public static int SharedTimerInterval = 2000; //milliseconds
        public static TimeSpan DefaultMaxBatchDelay = TimeSpan.FromSeconds(5);



        public static ITableBatchGrouper CreateBatchGrouper(this CloudTable cloudTable)
        {
            return new TableBatchGrouperImpl(cloudTable);
        }
    }
}
