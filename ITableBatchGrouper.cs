using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading.Tasks;
namespace AzureStorage.TableBatchGrouper
{
    public interface ITableBatchGrouper
    {
        Task<TableResult> Delete(ITableEntity entity);
        Task<TableResult> Insert(ITableEntity entity);
        Task<TableResult> InsertOrMerge(ITableEntity entity);
        Task<TableResult> InsertOrReplace(ITableEntity entity);
        Task<TableResult> Merge(ITableEntity entity);
        Task<TableResult> Replace(ITableEntity entity);
        void SetMaxDelayForPartition(string partition, TimeSpan delay);
        void Dispose();
    }
}
