﻿namespace NServiceBus.AcceptanceTests.Diagnostics
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_enabling_diagnostics : NServiceBusAcceptanceTest
    {
        //TODO test outgoing
        //TODO test "disabled" behavior?
        //TODO should these tests be moved to the Core test folder to not be shipped to downstreams?



        [Test]
        public async Task Should_capture_outgoing_message_traces()
        {
            var activityListener = TestingActivityListener.SetupNServiceBusDiagnosticListener();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<TestEndpoint>(b => b
                    .CustomConfig(c => c.ConfigureRouting().RouteToEndpoint(typeof(IncomingMessage), typeof(ReceivingEndpoint)))
                    .When(s => s.Send(new IncomingMessage())))
                .WithEndpoint<ReceivingEndpoint>()
                .Done(c => c.IncomingMessageReceived)
                .Run();

            Assert.AreEqual(activityListener.CompletedActivities.Count, activityListener.StartedActivities.Count, "all activities should be completed");

            var incomingMessageActivities = activityListener.CompletedActivities.FindAll(a => a.OperationName == "NServiceBus.Diagnostics.OutgoingMessage");
            Assert.AreEqual(1, incomingMessageActivities.Count, "1 message is being sent");
            Assert.AreEqual(context.IncomingMessageId, incomingMessageActivities[0].Tags.ToImmutableDictionary()["NServiceBus.MessageId"]);
            Assert.IsNull(incomingMessageActivities[0].ParentId);
        }

        class Context : ScenarioContext
        {
            public bool OutgoingMessageReceived { get; set; }
            public string IncomingMessageId { get; set; }
            public string OutgoingMessageId { get; set; }
            public bool IncomingMessageReceived { get; set; }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint() => EndpointSetup<DefaultServer>();

            class MessageHandler : IHandleMessages<IncomingMessage>
            {
                readonly Context testContext;

                public MessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(IncomingMessage message, IMessageHandlerContext context)
                {
                    testContext.IncomingMessageId = context.MessageId;
                    testContext.IncomingMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        class ReplyingEndpoint : EndpointConfigurationBuilder
        {
            public ReplyingEndpoint() => EndpointSetup<DefaultServer>(c => c.ConfigureRouting().RouteToEndpoint(typeof(OutgoingMessage), typeof(TestEndpoint)));

            class MessageHandler : IHandleMessages<IncomingMessage>
            {
                readonly Context testContext;

                public MessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(IncomingMessage message, IMessageHandlerContext context)
                {
                    testContext.IncomingMessageId = context.MessageId;
                    testContext.IncomingMessageReceived = true;
                    return context.Send(new OutgoingMessage()); //TODO change this to reply
                }
            }
        }

        class TestEndpoint : EndpointConfigurationBuilder
        {
            public TestEndpoint() => EndpointSetup<DefaultServer>();

            class MessageHandler : IHandleMessages<OutgoingMessage>
            {
                Context testContext;

                public MessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(OutgoingMessage message, IMessageHandlerContext context)
                {
                    testContext.OutgoingMessageId = context.MessageId;
                    testContext.OutgoingMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class IncomingMessage : IMessage
        {

        }

        public class OutgoingMessage : IMessage
        {

        }
    }
}