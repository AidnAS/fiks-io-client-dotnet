using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KS.Fiks.IO.Client.Amqp;
using KS.Fiks.IO.Client.Configuration;
using KS.Fiks.IO.Client.Dokumentlager;
using KS.Fiks.IO.Client.Send;
using KS.Fiks.IO.Send.Client.Configuration;
using Ks.Fiks.Maskinporten.Client;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace KS.Fiks.IO.Client.Tests.Amqp
{
    public class AmqpHandlerFixture
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private bool _connectionFactoryShouldThrow;
        private bool _connectionShouldThrow;
        private string _token = "testtoken";
        private Guid _integrationId = Guid.NewGuid();
        private string _integrationPassword = "defaultPassword";

        public AmqpHandlerFixture WhereConnectionfactoryThrowsException()
        {
            _connectionFactoryShouldThrow = true;
            return this;
        }

        public AmqpHandlerFixture WhereConnectionThrowsException()
        {
            _connectionShouldThrow = true;
            return this;
        }

        public AmqpHandlerFixture WithMaskinportenToken(string token)
        {
            _token = token;
            return this;
        }

        public AmqpHandlerFixture WithIntegrationPassword(string password)
        {
            _integrationPassword = password;
            return this;
        }

        public AmqpHandlerFixture WithIntegrationId(Guid id)
        {
            _integrationId = id;
            return this;
        }

        public Mock<IChannel> ModelMock { get; } = new Mock<IChannel>();

        public Mock<IConnectionFactory> ConnectionFactoryMock { get; } = new Mock<IConnectionFactory>();

        public Mock<IConnection> ConnectionMock { get; } = new Mock<IConnection>();

        internal Mock<IAmqpConsumerFactory> AmqpConsumerFactoryMock { get; } = new Mock<IAmqpConsumerFactory>();

        internal Mock<IAmqpReceiveConsumer> AmqpReceiveConsumerMock { get; } = new Mock<IAmqpReceiveConsumer>();

        internal Mock<IMaskinportenClient> MaskinportenClientMock { get; } = new Mock<IMaskinportenClient>();

        internal Mock<IDokumentlagerHandler> DokumentlagerHandlerMock { get; } = new Mock<IDokumentlagerHandler>();

        internal Mock<ISendHandler> SendHandlerMock { get; } = new Mock<ISendHandler>();

        internal IAmqpHandler CreateSut()
        {
            SetupMocks();
            var amqpConfiguration = CreateConfiguration();
            var amqpHandler = AmqpHandler.CreateAsync(
                    MaskinportenClientMock.Object,
                     SendHandlerMock.Object,
                     DokumentlagerHandlerMock.Object,
                     amqpConfiguration,
                     CreateIntegrationConfiguration(),
                     new KontoConfiguration(_accountId, "dummy"),
                     null,
                     ConnectionFactoryMock.Object,
                     AmqpConsumerFactoryMock.Object).Result;

            return amqpHandler;
        }

        internal Task<IAmqpHandler> CreateSutAsync()
        {
            SetupMocks();
            var amqpConfiguration = CreateConfiguration();
            var amqpHandler = AmqpHandler.CreateAsync(
                MaskinportenClientMock.Object,
                SendHandlerMock.Object,
                DokumentlagerHandlerMock.Object,
                amqpConfiguration,
                CreateIntegrationConfiguration(),
                new KontoConfiguration(_accountId, "dummy"),
                null,
                ConnectionFactoryMock.Object,
                AmqpConsumerFactoryMock.Object);

            return amqpHandler;
        }

        private static AmqpConfiguration CreateConfiguration()
        {
            return new AmqpConfiguration("api.fiks.test.ks.no");
        }

        private void SetupMocks()
        {
            if (_connectionFactoryShouldThrow)
            {
                ConnectionFactoryMock.Setup(_ => _.CreateConnectionAsync(It.IsAny<IList<AmqpTcpEndpoint>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                     .Throws<ProtocolViolationException>();
            }
            else
            {
                ConnectionFactoryMock.Setup(_ => _.CreateConnectionAsync(It.IsAny<IList<AmqpTcpEndpoint>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                     .Returns(Task.FromResult(ConnectionMock.Object));
            }

            ConnectionFactoryMock.SetupSet(_ => _.Password = It.IsAny<string>());
            ConnectionFactoryMock.SetupSet(_ => _.UserName = It.IsAny<string>());

            if (_connectionShouldThrow)
            {
                ConnectionMock.Setup(_ => _.CreateChannelAsync(null, CancellationToken.None)).Throws<ProtocolViolationException>();
            }
            else
            {
                ConnectionMock.Setup(_ => _.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(ModelMock.Object));
            }

            AmqpConsumerFactoryMock.Setup(_ => _.CreateReceiveConsumer(It.IsAny<IChannel>()))
                                   .Returns(AmqpReceiveConsumerMock.Object);

            MaskinportenClientMock.Setup(_ => _.GetAccessToken(It.IsAny<string>()))
                                  .ReturnsAsync(new MaskinportenToken(_token, 100));
        }

        private IntegrasjonConfiguration CreateIntegrationConfiguration()
        {
            return new IntegrasjonConfiguration(_integrationId, _integrationPassword);
        }
    }
}