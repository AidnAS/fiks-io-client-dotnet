using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace KS.Fiks.IO.Client.Amqp
{
    using System;
    using Microsoft.Extensions.Logging;

    internal class DefaultAmqpWatcher : IAmqpWatcher
    {
        private readonly ILogger<DefaultAmqpWatcher> _logger;

        public DefaultAmqpWatcher(ILoggerFactory loggerFactory = null)
        {
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<DefaultAmqpWatcher>();
            }
        }

        public Task HandleConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            _logger?.LogDebug("RabbitMQ Connection ConnectionBlocked event has been triggered");
            return Task.CompletedTask;
        }

        public Task HandleConnectionShutdown(object sender, ShutdownEventArgs shutdownEventArgs)
        {
            _logger?.LogDebug($"RabbitMQ Connection ConnectionShutdown event has been triggered");
            return Task.CompletedTask;
        }

        public Task HandleConnectionUnblocked(object sender, AsyncEventArgs e)
        {
            _logger?.LogDebug("RabbitMQ Connection ConnectionUnblocked event has been triggered");
            return Task.CompletedTask;
        }
    }
}
