# AzureStorage.TableBatchGrouper
Makes Azure Storage Tables Batching seemless.

##Usage

Once the project is referenced, an extension method is added to CloudTable:
```csharp

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
        
        void SetMaxDelayForPartition(string partition, TimeSpan delay);
        void Dispose();
    }
```

An ITableBatchGrouper instance is thread safe, and may be used in a very long life cycle context.

##Configuration

Internaly, all ITableBatchGrouper instance (implemented internaly by TableBatchGrouperImpl) share the same System.Timers.Timer instance, and will dequeue incoming operations on a common tick.
By default the timer Interval is set to 2 seconds. You may want to change that:

```csharp
   //before the first Batchgrouper instanciation:
   TableBatchGrouperExtensions.SharedTimerInterval = 2000;
```

In azure Table Storage, batch are limited to 100 operations on the same partition.
Hence, the Batch operation is launched either when 100 operations is enqueued, or when the maximum wait delay is reached.
By default, this delay is set to 5 seconds, you may want to change that as well:
```csharp
   TableBatchGrouperExtensions.DefaultMaxBatchDelay = TimeSpan.FromSeconds(10);
```

This delay can be settled for specific partitions as well using ITableBatchGrouper.SetMaxDelayForPartition
