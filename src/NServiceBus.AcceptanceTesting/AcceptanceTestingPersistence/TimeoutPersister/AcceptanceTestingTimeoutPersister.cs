namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Timeout.Core;

    class AcceptanceTestingTimeoutPersister : IPersistTimeouts, IQueryTimeouts, IDisposable
    {
        public AcceptanceTestingTimeoutPersister(Func<DateTimeOffset> currentTimeProvider)
        {
            this.currentTimeProvider = currentTimeProvider;
        }

        public void Dispose()
        {
        }

        public Task Add(TimeoutData timeout, ContextBag context)
        {
            timeout.Id = Guid.NewGuid().ToString();
            try
            {
                readerWriterLock.EnterWriteLock();
                storage.Add(timeout);
            }
            finally
            {
                readerWriterLock.ExitWriteLock();
            }

            return Task.CompletedTask;
        }

        public Task<TimeoutData> Peek(string timeoutId, ContextBag context)
        {
            try
            {
                readerWriterLock.EnterReadLock();
                return Task.FromResult(storage.SingleOrDefault(t => t.Id.ToString() == timeoutId));
            }
            finally
            {
                readerWriterLock.ExitReadLock();
            }
        }

        public Task<bool> TryRemove(string timeoutId, ContextBag context)
        {
            try
            {
                readerWriterLock.EnterWriteLock();

                for (var index = 0; index < storage.Count; index++)
                {
                    var data = storage[index];
                    if (data.Id == timeoutId)
                    {
                        storage.RemoveAt(index);
                        return TaskEx.TrueTask;
                    }
                }

                return TaskEx.FalseTask;
            }
            finally
            {
                readerWriterLock.ExitWriteLock();
            }
        }

        public Task RemoveTimeoutBy(Guid sagaId, ContextBag context)
        {
            try
            {
                readerWriterLock.EnterWriteLock();
                for (var index = 0; index < storage.Count;)
                {
                    var timeoutData = storage[index];
                    if (timeoutData.SagaId == sagaId)
                    {
                        storage.RemoveAt(index);
                        continue;
                    }
                    index++;
                }
            }
            finally
            {
                readerWriterLock.ExitWriteLock();
            }

            return Task.CompletedTask;
        }

        public Task<TimeoutsChunk> GetNextChunk(DateTimeOffset startSlice)
        {
            var now = currentTimeProvider();
            var nextTimeToRunQuery = DateTimeOffset.MaxValue;
            var dueTimeouts = new List<TimeoutsChunk.Timeout>();

            try
            {
                readerWriterLock.EnterReadLock();

                foreach (var data in storage)
                {
                    if (data.Time > now && data.Time < nextTimeToRunQuery)
                    {
                        nextTimeToRunQuery = data.Time;
                    }
                    if (data.Time > startSlice && data.Time <= now)
                    {
                        dueTimeouts.Add(new TimeoutsChunk.Timeout(data.Id, data.Time));
                    }
                }
            }
            finally
            {
                readerWriterLock.ExitReadLock();
            }

            if (nextTimeToRunQuery == DateTimeOffset.MaxValue)
            {
                nextTimeToRunQuery = now.Add(EmptyResultsNextTimeToRunQuerySpan);
            }

            return Task.FromResult(new TimeoutsChunk(dueTimeouts.ToArray(), nextTimeToRunQuery));
        }

        Func<DateTimeOffset> currentTimeProvider;
        ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim();
        List<TimeoutData> storage = new List<TimeoutData>();

        static TimeSpan EmptyResultsNextTimeToRunQuerySpan = TimeSpan.FromMinutes(1);
    }
}