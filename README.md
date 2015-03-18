# AzureStorage.TableBatchGrouper
Makes Azure Storage Tables Batching seemless.

##Usage

Once the project is referenced, an extension method is added to CloudTable:
```C#

CloudTable someTable;
ITableBatchGrouper grouper = someTable.CreateBatchGrouper();
```

from an ITableBatchGrouper instance, table operations can be executed in an Atomic way, but are all batched internaly:

```csharp
    public interface ITableBatchGrouper
    {
        Task<TableResult> Delete(ITableEntity entity);
        Task<TableResult> Insert(ITableEntity entity);
        Task<TableResult> InsertOrMerge(ITableEntity entity);
        Task<TableResult> InsertOrReplace(ITableEntity entity);
        Task<TableResult> Merge(ITableEntity entity);
        Task<TableResult> Replace(ITableEntity entity);
        void Dispose();
    }
```
