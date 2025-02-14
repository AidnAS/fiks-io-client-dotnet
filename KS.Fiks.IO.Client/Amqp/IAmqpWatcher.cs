using RabbitMQ.Client.Events;
using System;
using System.Threading.Tasks;

namespace KS.Fiks.IO.Client.Amqp
{
    public interface IAmqpWatcher
    {
        Task HandleConnectionBlocked(object sender, ConnectionBlockedEventArgs e);
        Task HandleConnectionShutdown(object sender, ShutdownEventArgs shutdownEventArgs);
        Task HandleConnectionUnblocked(object sender, AsyncEventArgs e);
    }
}