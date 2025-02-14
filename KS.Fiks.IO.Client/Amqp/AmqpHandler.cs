using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading.Tasks;
using KS.Fiks.IO.Client.Configuration;
using KS.Fiks.IO.Client.Dokumentlager;
using KS.Fiks.IO.Client.Exceptions;
using KS.Fiks.IO.Client.Models;
using KS.Fiks.IO.Client.Send;
using KS.Fiks.IO.Send.Client.Configuration;
using Ks.Fiks.Maskinporten.Client;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KS.Fiks.IO.Client.Amqp
{
    internal class AmqpHandler : IAmqpHandler
    {
        private const string QueuePrefix = "fiksio.konto.";
        private static ILogger<AmqpHandler> _logger;
        private readonly IAmqpConsumerFactory _amqpConsumerFactory;
        private readonly KontoConfiguration _kontoConfiguration;
        private readonly SslOption _sslOption;
        private readonly IConnectionFactory _connectionFactory;
        private readonly IAmqpWatcher _amqpWatcher;
        private IConnection _connection;
        private IChannel _channel;
        private IAmqpReceiveConsumer _receiveConsumer;
        private EventHandler<MottattMeldingArgs> _receivedEvent;
        private EventHandler<ConsumerEventArgs> _cancelledEvent;

        private AmqpHandler(
            IMaskinportenClient maskinportenClient,
            ISendHandler sendHandler,
            IDokumentlagerHandler dokumentlagerHandler,
            AmqpConfiguration amqpConfiguration,
            IntegrasjonConfiguration integrasjonConfiguration,
            KontoConfiguration kontoConfiguration,
            ILoggerFactory loggerFactory = null,
            IConnectionFactory connectionFactory = null,
            IAmqpConsumerFactory consumerFactory = null,
            IAmqpWatcher amqpWatcher = null)
        {
            _sslOption = amqpConfiguration.SslOption ?? new SslOption();
            _kontoConfiguration = kontoConfiguration;
            _connectionFactory = connectionFactory ?? new ConnectionFactory
            {
                CredentialsProvider = new MaskinportenCredentialsProvider("TokenCredentialsForMaskinporten", maskinportenClient, integrasjonConfiguration, loggerFactory)
            };

            if (!string.IsNullOrEmpty(amqpConfiguration.Vhost))
            {
                _connectionFactory.VirtualHost = amqpConfiguration.Vhost;
            }

            _amqpConsumerFactory = consumerFactory ?? new AmqpConsumerFactory(sendHandler, dokumentlagerHandler, _kontoConfiguration);

            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<AmqpHandler>();
            }

            _amqpWatcher = amqpWatcher ?? new DefaultAmqpWatcher(loggerFactory);
        }

        public static async Task<IAmqpHandler> CreateAsync(
            IMaskinportenClient maskinportenClient,
            ISendHandler sendHandler,
            IDokumentlagerHandler dokumentlagerHandler,
            AmqpConfiguration amqpConfiguration,
            IntegrasjonConfiguration integrasjonConfiguration,
            KontoConfiguration kontoConfiguration,
            ILoggerFactory loggerFactory = null,
            IConnectionFactory connectionFactory = null,
            IAmqpConsumerFactory consumerFactory = null,
            IAmqpWatcher amqpWatcher = null)
        {
            var amqpHandler = new AmqpHandler(maskinportenClient, sendHandler, dokumentlagerHandler, amqpConfiguration, integrasjonConfiguration, kontoConfiguration, loggerFactory, connectionFactory, consumerFactory, amqpWatcher);
            await amqpHandler.Connect(amqpConfiguration).ConfigureAwait(false);

             _logger?.LogDebug("AmqpHandler CreateAsync done");
            return amqpHandler;
        }

        public void AddMessageReceivedHandler(
            EventHandler<MottattMeldingArgs> receivedEvent,
            EventHandler<ConsumerEventArgs> cancelledEvent)
        {
            _cancelledEvent = cancelledEvent;
            _receivedEvent = receivedEvent;

            if (_receiveConsumer == null)
            {
                _receiveConsumer = _amqpConsumerFactory.CreateReceiveConsumer(_channel);
            }

            _receiveConsumer.Received += receivedEvent;
            //_receiveConsumer.ConsumerCancelled += cancelledEvent;

            _channel.BasicConsumeAsync(GetQueueName(), false, _receiveConsumer);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsOpen()
        {
            _logger?.LogDebug($"IsOpen status _channel: {_channel} and _connection: {_connection}");
            return _channel != null && _channel.IsOpen && _connection != null && _connection.IsOpen;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            // Unsubscribe consumer events
            if (_receivedEvent != null)
            {
                _receiveConsumer.Received -= _receivedEvent;
            }

            if (_cancelledEvent != null)
            {
                //_receiveConsumer.ConsumerCancelledAync -= _cancelledEvent;
            }

            // Unsubscribe events for logging of RabbitMQ events
            _connection.ConnectionShutdownAsync -= _amqpWatcher.HandleConnectionShutdown;
            _connection.ConnectionBlockedAsync -= _amqpWatcher.HandleConnectionBlocked;
            _connection.ConnectionUnblockedAsync -= _amqpWatcher.HandleConnectionUnblocked;

            _channel.Dispose();
            _connection.Dispose();
        }

        private async Task Connect(AmqpConfiguration amqpConfiguration)
        {
            _connection = await CreateConnection(amqpConfiguration);
            _channel = await ConnectToChannel(amqpConfiguration);

            // Handle events for logging of RabbitMQ events
            _connection.ConnectionShutdownAsync += _amqpWatcher.HandleConnectionShutdown;
            _connection.ConnectionBlockedAsync += _amqpWatcher.HandleConnectionBlocked;
            _connection.ConnectionUnblockedAsync += _amqpWatcher.HandleConnectionUnblocked;
        }

        private async Task<IChannel> ConnectToChannel(AmqpConfiguration configuration)
        {
            try
            {
                var channel = await _connection.CreateChannelAsync();
                await channel.BasicQosAsync(0, configuration.PrefetchCount, false);
                return channel;
            }
            catch (Exception ex)
            {
                throw new FiksIOAmqpConnectionFailedException("Unable to connect to channel", ex);
            }
        }

        private async Task<IConnection> CreateConnection(AmqpConfiguration configuration)
        {
            try
            {
                var endpoint = new AmqpTcpEndpoint(configuration.Host, configuration.Port, _sslOption);
                var connection = await _connectionFactory.CreateConnectionAsync(new List<AmqpTcpEndpoint> { endpoint }, configuration.ApplicationName);
                return connection;
            }
            catch (Exception ex)
            {
                throw new FiksIOAmqpConnectionFailedException($"Unable to create connection. Host: {configuration.Host}; Port: {configuration.Port}; UserName:{_connectionFactory.UserName}; SslOption.Enabled: {_sslOption?.Enabled};SslOption.ServerName: {_sslOption?.ServerName}", ex);
            }
        }

        private string GetQueueName()
        {
            return $"{QueuePrefix}{_kontoConfiguration.KontoId}";
        }
    }
}