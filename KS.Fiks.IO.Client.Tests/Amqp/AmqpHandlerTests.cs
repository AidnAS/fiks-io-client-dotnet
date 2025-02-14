using System;
using System.Collections.Generic;
using System.Threading;
using KS.Fiks.IO.Client.Exceptions;
using KS.Fiks.IO.Client.Models;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;
using Xunit;

namespace KS.Fiks.IO.Client.Tests.Amqp
{
    public class AmqpHandlerTests
    {
        private readonly AmqpHandlerFixture _fixture;

        public AmqpHandlerTests()
        {
            _fixture = new AmqpHandlerFixture();
        }

        [Fact]
        public void CreatesModelWhenConstructed()
        {
            var sut = _fixture.CreateSut();

            _fixture.ConnectionFactoryMock.Verify(_ => _.CreateConnectionAsync(It.IsAny<IList<AmqpTcpEndpoint>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _fixture.ConnectionMock.Verify(_ => _.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async System.Threading.Tasks.Task ThrowsExceptionWhenConnectionFactoryThrows()
        {
            await Assert.ThrowsAsync<FiksIOAmqpConnectionFailedException>(() =>
                _fixture.WhereConnectionfactoryThrowsException().CreateSutAsync());
        }

        [Fact]
        public async System.Threading.Tasks.Task ThrowsExceptionWhenConnectionThrows()
        {
            await Assert.ThrowsAsync<FiksIOAmqpConnectionFailedException>(() =>
                _fixture.WhereConnectionThrowsException().CreateSutAsync());
        }

        [Fact]
        public void AddReceivedListenerCreatesNewConsumer()
        {
            var sut = _fixture.CreateSut();

            var handler = new EventHandler<MottattMeldingArgs>((a, _) => { });

            sut.AddMessageReceivedHandler(handler, null);

            _fixture.AmqpConsumerFactoryMock.Verify(_ => _.CreateReceiveConsumer(It.IsAny<IChannel>()));
        }

        [Fact]
        public void AddReceivedListenerAddsHandlerToReceivedEvent()
        {
            var sut = _fixture.CreateSut();

            var counter = 0;
            var handler = new EventHandler<MottattMeldingArgs>((a, _) => { counter++; });

            sut.AddMessageReceivedHandler(handler, null);

            _fixture.AmqpReceiveConsumerMock.Raise(_ => _.Received += null, this, null);
            counter.ShouldBe(1);
        }

    }
}