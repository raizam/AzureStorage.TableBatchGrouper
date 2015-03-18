using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AzureStorage.TableBatchGrouper
{
    internal class TableBatchGrouperImpl : IDisposable, ITableBatchGrouper
    {
        readonly CloudTable table;
        static System.Timers.Timer sharedTimer = null;
        static int instanceCount = 0;
        readonly static object staticLock = new object();

        public TableBatchGrouperImpl(CloudTable table)
        {
            this.table = table;

            lock (staticLock)
            {
                if (instanceCount++ == 0)
                {
                    sharedTimer = new System.Timers.Timer(TableBatchGrouperExtensions.SharedTimerInterval);
                    sharedTimer.AutoReset = true;
                    sharedTimer.Start();
                }

            }

            sharedTimer.Elapsed += dequeueAndProcessBatches;

        }

        ~TableBatchGrouperImpl()
        {
            Dispose(false);
        }

        volatile bool isProcessing = false;
        void dequeueAndProcessBatches(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (isProcessing)
                return;
            isProcessing = true;
            try
            {

                TableOperationResultPair operation;
                while (incomingRequests.TryDequeue(out operation) && !isDisposing)
                {

                    PendingBatch pendingBatch = allPendingBatches.FirstOrDefault(w => w.partition == operation.partition);

                    if (pendingBatch == null)
                    {
                        var customPartitionDelay = customPartitionMaxDelay.FirstOrDefault(w => w.Item1 == operation.partition);

                        pendingBatch = new PendingBatch(operation.partition, customPartitionDelay == null ? TableBatchGrouperExtensions.DefaultMaxBatchDelay : customPartitionDelay.Item2);
                        allPendingBatches.Add(pendingBatch);
                    }

                    var pendingList = pendingBatch.pendingOperations;
                    pendingList.Add(operation);

                    if (pendingList.Count == 100)
                    {
                        executeBatch(pendingBatch);
                    }
                }

                var expiredBatches = allPendingBatches.Where(w => w.Timeout < DateTime.Now);

                foreach (var expired in expiredBatches.ToArray())
                {
                    executeBatch(expired);
                }
            }
            finally
            {
                isProcessing = false;
            }
        }


        internal class TableOperationResultPair
        {
            public TableOperationResultPair(string partition, TableOperation operation)
            {
                this.operation = operation;
                this.partition = partition;
            }
            public readonly TableOperation operation;
            public readonly string partition;
            public readonly TaskCompletionSource<TableResult> taskSource = new TaskCompletionSource<TableResult>();

        }
        internal class PendingBatch
        {
            public PendingBatch(string partition, TimeSpan timeout)
            {

                this.partition = partition;
                this.Timeout = DateTime.Now.Add(timeout);
            }
            public readonly DateTime Timeout;
            public readonly string partition;
            public readonly List<TableOperationResultPair> pendingOperations = new List<TableOperationResultPair>();
        }


        volatile bool isDisposing = false;

        List<PendingBatch> allPendingBatches = new List<PendingBatch>();


        CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private void CancelAll()
        {

            TableOperationResultPair toCancel;
            while (incomingRequests.TryDequeue(out toCancel))
            {
                toCancel.taskSource.TrySetCanceled();
            }

            foreach (var pending in allPendingBatches)
            {
                foreach (var operationToCancel in pending.pendingOperations)
                {
                    operationToCancel.taskSource.TrySetCanceled();
                }
            }
        }

        private void executeBatch(PendingBatch pendingBatch)
        {
            allPendingBatches.Remove(pendingBatch);

            __executeBatch(pendingBatch);
        }

        private void __executeBatch(PendingBatch pendingBatch)
        {
            if (pendingBatch.pendingOperations.Count == 0)
                return;
            if (cancellationSource.IsCancellationRequested)
            {
                pendingBatch.pendingOperations.ForEach(opPair => opPair.taskSource.TrySetCanceled());
                return;
            }


            TableBatchOperation batchOperation = new TableBatchOperation();

            foreach (var op in pendingBatch.pendingOperations)
            {
                batchOperation.Add(op.operation);
            }

            TableOperationResultPair[] processingOperations = pendingBatch.pendingOperations.ToArray();

            table.ExecuteBatchAsync(batchOperation, cancellationSource.Token).ContinueWith(taskResult =>
            {
                try
                {
                    TableResult[] tableResults = taskResult.Result.ToArray();


                    for (int i = 0; i < processingOperations.Length; i++)
                    {
                        processingOperations[i].taskSource.TrySetResult(tableResults[i]);
                    }
                }
                catch (AggregateException aggr)
                {
                    var storageEx = aggr.InnerException as StorageException; 

                    int failedIndex;
                    if (storageEx != null && int.TryParse(storageEx.RequestInformation.ExtendedErrorInformation.ErrorMessage.Split(':').First(), out failedIndex))
                    {
                        var failed = pendingBatch.pendingOperations[failedIndex];
                        failed.taskSource.TrySetException(storageEx);
                        pendingBatch.pendingOperations.RemoveAt(failedIndex);
                        __executeBatch(pendingBatch);
                    }
                    else
                    {
                        for (int i = 0; i < processingOperations.Length; i++)
                        {
                            processingOperations[i].taskSource.TrySetException(storageEx);
                        }
                    }
                }
                catch (Exception ex)
                {
                    for (int i = 0; i < processingOperations.Length; i++)
                    {
                        processingOperations[i].taskSource.TrySetException(ex);
                    }
                }

            });
        }

        private Task<TableResult> Enqueue(string partition, TableOperation operation)
        {

            if (isDisposing)
                throw new ObjectDisposedException(this.GetType().Name);

            TableOperationResultPair op = new TableOperationResultPair(partition, operation);
            incomingRequests.Enqueue(op);
            return op.taskSource.Task;
        }

        System.Collections.Concurrent.ConcurrentQueue<TableOperationResultPair> incomingRequests = new System.Collections.Concurrent.ConcurrentQueue<TableOperationResultPair>();

        private void Dispose(bool isExplicitDispose)
        {
            if (isDisposing)
                return;

            isDisposing = true;

            sharedTimer.Elapsed -= dequeueAndProcessBatches;

            cancellationSource.Cancel();
            CancelAll();


            lock (staticLock)
            {
                if (--instanceCount == 0)
                {
                    sharedTimer.Stop();
                    sharedTimer.Dispose();
                    sharedTimer = null;
                }

            }
        }

        #region Public Methods

        public void Dispose()
        {
            Dispose(true);
        }


        public Task<TableResult> Insert(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.Insert(entity));
        }

        public Task<TableResult> InsertOrMerge(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.InsertOrMerge(entity));
        }

        public Task<TableResult> InsertOrReplace(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.InsertOrReplace(entity));
        }

        public Task<TableResult> Merge(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.Merge(entity));
        }

        public Task<TableResult> Replace(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.Replace(entity));
        }

        public Task<TableResult> Delete(ITableEntity entity)
        {
            return Enqueue(entity.PartitionKey, TableOperation.Delete(entity));
        }


        List<Tuple<string, TimeSpan>> customPartitionMaxDelay = new List<Tuple<string, TimeSpan>>();
        public void SetMaxDelayForPartition(string partition, TimeSpan delay)
        {
            lock (customPartitionMaxDelay)
            {
                var currentDelay = customPartitionMaxDelay.FirstOrDefault(w => w.Item1 == partition);
                if (currentDelay != null)
                    customPartitionMaxDelay.Remove(currentDelay);

                customPartitionMaxDelay.Add(new Tuple<string,TimeSpan>(partition, delay));
            }
        }

        #endregion //Public Methods
    }


}
