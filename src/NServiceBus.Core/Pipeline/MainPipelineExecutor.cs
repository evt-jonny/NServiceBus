namespace NServiceBus
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Diagnostics;
    using Pipeline;
    using Transport;

    class MainPipelineExecutor : IPipelineExecutor
    {
        const string IncomingMessageActivityName = "NServiceBus.Diagnostics.IncomingMessage";

        public MainPipelineExecutor(IServiceProvider rootBuilder, IPipelineCache pipelineCache, MessageOperations messageOperations, INotificationSubscriptions<ReceivePipelineCompleted> receivePipelineNotification, Pipeline<ITransportReceiveContext> receivePipeline)
        {
            this.rootBuilder = rootBuilder;
            this.pipelineCache = pipelineCache;
            this.messageOperations = messageOperations;
            this.receivePipelineNotification = receivePipelineNotification;
            this.receivePipeline = receivePipeline;
        }

        public async Task Invoke(MessageContext messageContext, CancellationToken cancellationToken = default)
        {
            var pipelineStartedAt = DateTimeOffset.UtcNow;

            using var activity = CreateIncomingActivity(messageContext);

            using (var childScope = rootBuilder.CreateScope())
            {
                var message = new IncomingMessage(messageContext.NativeMessageId, messageContext.Headers, messageContext.Body);
                AddTraceInfo(activity, message);
                activity?.AddTag("NServiceBus.MessageId", message.MessageId);

                var rootContext = new RootContext(childScope.ServiceProvider, messageOperations, pipelineCache, cancellationToken);
                rootContext.Extensions.Merge(messageContext.Extensions);

                var transportReceiveContext = new TransportReceiveContext(message, messageContext.TransportTransaction, rootContext);

                try
                {
                    await receivePipeline.Invoke(transportReceiveContext).ConfigureAwait(false);
                }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException - enriching and rethrowing
                catch (Exception ex)
#pragma warning restore PS0019 // Do not catch Exception without considering OperationCanceledException
                {
                    ex.Data["Message ID"] = message.MessageId;

                    if (message.NativeMessageId != message.MessageId)
                    {
                        ex.Data["Transport message ID"] = message.NativeMessageId;
                    }

                    ex.Data["Pipeline canceled"] = transportReceiveContext.CancellationToken.IsCancellationRequested;

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;
                }

                await receivePipelineNotification.Raise(new ReceivePipelineCompleted(message, pipelineStartedAt, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.Dispose(); //TODO ensure disposal. Set acitivity state.
        }

        void AddTraceInfo(Activity activity, IncomingMessage message)
        {
            if (activity == null)
            {
                return;
            }

            var operation = "process";
            // TODO: Set destination properly
            var destination = "ReceivingEndpoint";
            activity.DisplayName = $"{destination} {operation}";
            activity.AddTag("messaging.operation", operation);
            activity.AddTag("messaging.destination", destination);
            activity.AddTag("messaging.message_id", message.MessageId);
            activity.AddTag("messaging.message_payload_size_bytes", message.Body.Length.ToString());
            if (message.Headers.TryGetValue(Headers.ConversationId, out var conversationId))
            {
                activity.AddTag("messaging.conversation_id", conversationId);
            }
        }

        static Activity CreateIncomingActivity(MessageContext context)
        {
            //TODO Do we need to check for Activity.Current first in case the transport creates it's own span?
            var activity = context.Headers.TryGetValue("traceparent", out var parentId)
                ? ActivitySources.Main.StartActivity(name: IncomingMessageActivityName, ActivityKind.Consumer, parentId)
                : ActivitySources.Main.StartActivity(name: IncomingMessageActivityName, ActivityKind.Consumer);

            if (activity != null)
            {
                context.Headers.TryGetValue("tracestate", out var traceState);
                activity.TraceStateString = traceState;
            }

            return activity;
        }

        readonly IServiceProvider rootBuilder;
        readonly IPipelineCache pipelineCache;
        readonly MessageOperations messageOperations;
        readonly INotificationSubscriptions<ReceivePipelineCompleted> receivePipelineNotification;
        readonly Pipeline<ITransportReceiveContext> receivePipeline;
    }
}