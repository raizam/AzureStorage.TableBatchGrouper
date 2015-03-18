using System;
namespace AzureStorage.TableBatchGrouper
{
    public interface ITableBatchGrouper
    {
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> Delete(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
        void Dispose();
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> Insert(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> InsertOrMerge(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> InsertOrReplace(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> Merge(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
        System.Threading.Tasks.Task<Microsoft.WindowsAzure.Storage.Table.TableResult> Replace(Microsoft.WindowsAzure.Storage.Table.ITableEntity entity);
    }
}
