using System;
using System.Threading.Tasks;

namespace KS.Fiks.IO.Client.Amqp
{
    public interface IAmqpAcknowledgeManager
    {
        Task Ack();

        Task Nack();

        Task NackWithRequeue();
    }

    public class AmqpAcknowledgeManager : IAmqpAcknowledgeManager
    {
        private readonly Func<ValueTask> _ackAction;
        private readonly Func<ValueTask> _nackAction;
        private readonly Func<ValueTask> _nackWithRequeueAction;

        public AmqpAcknowledgeManager(Func<ValueTask> ackAction, Func<ValueTask> nackAction, Func<ValueTask> nackWithRequeueAction)
        {
            _ackAction = ackAction;
            _nackAction = nackAction;
            _nackWithRequeueAction = nackWithRequeueAction;
        }

        public async Task Ack()
        {
            if (_ackAction == null)
            {
                throw new NotSupportedException("Ack is currently not supported");
            }

            await _ackAction();
        }

        public async Task Nack()
        {
            if (_nackAction == null)
            {
                throw new NotSupportedException("Nack is currently not supported");
            }

            await _nackAction();
        }

        public async Task NackWithRequeue()
        {
            if (_nackWithRequeueAction == null)
            {
                throw new NotSupportedException("Nack is currently not supported");
            }

            await _nackWithRequeueAction();
        }
    }
}