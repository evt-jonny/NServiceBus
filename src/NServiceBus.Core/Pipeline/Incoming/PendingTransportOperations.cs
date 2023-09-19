namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using Transport;

    /// <summary>
    /// Represents the currently pending transport operations. The transport operations that are collected here will be
    /// dispatched in the batched dispatch stage of the pipeline.
    /// </summary>
    /// <remarks>This class is threadsafe.</remarks>
    public class PendingTransportOperations
    {
        /// <summary>
        /// Gets the currently pending transport operations.
        /// </summary>
        public TransportOperation[] Operations => operations.ToArray();

        /// <summary>
        /// Indicates whether there are transport operations pending.
        /// </summary>
        public bool HasOperations => !operations.IsEmpty;

        /// <summary>
        /// Adds a transport operation.
        /// </summary>
        /// <param name="transportOperation">The transport operation to be added.</param>
        public void Add(TransportOperation transportOperation)
        {
            ArgumentNullException.ThrowIfNull(transportOperation);

            operations.Push(transportOperation);
        }

        /// <summary>
        /// Adds a range of transport operations.
        /// </summary>
        /// <param name="transportOperations">The transport operations to be added.</param>
        public void AddRange(TransportOperation[] transportOperations)
        {
            if (transportOperations is null)
            {
                ThrowArgumentNullException(nameof(transportOperations));
            }
            if (transportOperations.Length == 0)
            {
                ThrowArgumentOutOfRangeException(nameof(transportOperations));
            }

            operations.PushRange(transportOperations);
            return;

            [DoesNotReturn]
            static void ThrowArgumentNullException(string paramName) => throw new ArgumentNullException(paramName);

            [DoesNotReturn]
            static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);
        }

        readonly ConcurrentStack<TransportOperation> operations = new ConcurrentStack<TransportOperation>();
    }
}