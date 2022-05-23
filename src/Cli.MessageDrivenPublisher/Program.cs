﻿namespace Cli.MessageDrivenPublisher
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using SqsMessages;

    class Program
    {
        static async Task Main()
        {
            Console.Title = "Cli.MessageDrivenPublisher";
            #region ConfigureEndpoint

            var endpointConfiguration = new EndpointConfiguration("Cli.MessageDrivenPublisher");
            var transportConfigration = endpointConfiguration.UseTransport<SqsTransport>();

            transportConfigration.Routing().RegisterPublisher(typeof(SampleEvent), "NativePubSubSqsPublisher");

            endpointConfiguration.Conventions().DefiningEventsAs(t => t == typeof(SampleEvent));

            endpointConfiguration.UsePersistence<InMemoryPersistence>();

            //transport.S3("bucketname", "my/key/prefix");

            #endregion

            endpointConfiguration.EnableInstallers();

            var endpointInstance = await Endpoint.Start(endpointConfiguration).ConfigureAwait(false);

            Console.WriteLine("Started");
            Console.ReadLine();

            await endpointInstance.Publish<SampleEvent>().ConfigureAwait(false);

            await endpointInstance.Stop().ConfigureAwait(false);
        }
    }
}