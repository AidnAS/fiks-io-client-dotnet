using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KS.Fiks.IO.Client.Dokumentlager;
using KS.Fiks.IO.Client.Exceptions;
using KS.Fiks.IO.Client.FileIO;
using KS.Fiks.IO.Client.Models;
using KS.Fiks.IO.Client.Send;
using KS.Fiks.IO.Client.Utility;
using KS.Fiks.IO.Crypto.Asic;
using RabbitMQ.Client;

namespace KS.Fiks.IO.Client.Amqp
{
    internal class AmqpReceiveConsumer : AsyncDefaultBasicConsumer, IAmqpReceiveConsumer
    {
        private const string DokumentlagerHeaderName = "dokumentlager-id";

        private readonly Guid _accountId;

        private readonly IAsicDecrypter _decrypter;

        private readonly IDokumentlagerHandler _dokumentlagerHandler;

        private readonly IFileWriter _fileWriter;

        private readonly ISendHandler _sendHandler;

        public AmqpReceiveConsumer(
            IChannel model,
            IDokumentlagerHandler dokumentlagerHandler,
            IFileWriter fileWriter,
            IAsicDecrypter decrypter,
            ISendHandler sendHandler,
            Guid accountId)
            : base(model)
        {
            this._dokumentlagerHandler = dokumentlagerHandler;
            this._fileWriter = fileWriter;
            this._decrypter = decrypter;
            this._sendHandler = sendHandler;
            this._accountId = accountId;
        }

        public event EventHandler<MottattMeldingArgs> Received;

        public override async Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        {
            await base.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body, cancellationToken);

            if (Received == null)
            {
                return;
            }

            try
            {
                var receivedMessage = ParseMessage(properties, body, redelivered);

                Received?.Invoke(
                    this,
                    new MottattMeldingArgs(receivedMessage, new SvarSender(_sendHandler, receivedMessage, new AmqpAcknowledgeManager(() => Channel.BasicAckAsync(deliveryTag, false), () => Channel.BasicNackAsync(deliveryTag, false, false), () => Channel.BasicNackAsync(deliveryTag, false, true)))));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private static bool IsDataInDokumentlager(IReadOnlyBasicProperties properties)
        {
            return ReceivedMessageParser.GetGuidFromHeader(properties.Headers, DokumentlagerHeaderName) != null;
        }

        private static Guid GetDokumentlagerId(IReadOnlyBasicProperties properties)
        {
            try
            {
                return ReceivedMessageParser.RequireGuidFromHeader(properties.Headers, DokumentlagerHeaderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private static bool HasPayload(IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            return IsDataInDokumentlager(properties) || body.Length > 0;
        }

        private MottattMelding ParseMessage(IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, bool resendt)
        {
            var metadata = ReceivedMessageParser.Parse(this._accountId, properties, resendt);
            return new MottattMelding(
                HasPayload(properties, body),
                metadata,
                GetDataProvider(properties, body.ToArray()),
                _decrypter,
                _fileWriter);
        }

        private Func<Task<Stream>> GetDataProvider(IReadOnlyBasicProperties properties, byte[] body)
        {
            if (!HasPayload(properties, body))
            {
                return () => throw new FiksIOMissingDataException("No data in message");
            }

            if (IsDataInDokumentlager(properties))
            {
                return async () => await this._dokumentlagerHandler.Download(GetDokumentlagerId(properties));
            }

            return async () => await Task.FromResult(new MemoryStream(body));
        }
    }
}