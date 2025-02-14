using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KS.Fiks.IO.Client.Exceptions;
using KS.Fiks.IO.Client.FileIO;
using KS.Fiks.IO.Client.Models;
using KS.Fiks.IO.Client.Utility;
using KS.Fiks.IO.Crypto.Asic;
using Moq;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace KS.Fiks.IO.Client.Tests.Amqp
{
    public class AmqpReceiveConsumerTests
    {
        private AmqpReceiveConsumerFixture _fixture;

        public AmqpReceiveConsumerTests()
        {
            _fixture = new AmqpReceiveConsumerFixture();
        }

        [Fact]
        public async Task ReceivedHandler()
        {
            var sut = _fixture.CreateSut();

            var hasBeenCalled = false;
            var handler = new EventHandler<MottattMeldingArgs>((a, _) => { hasBeenCalled = true; });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                Array.Empty<byte>());

            hasBeenCalled.ShouldBeTrue();
        }

        [Fact]
        public async Task ReceivesExpectedMessageMetadata()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.AvsenderKontoId.ToString()) },
                {"melding-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingId.ToString()) },
                {"type", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingType) },
                {"svar-til", Encoding.UTF8.GetBytes(expectedMessageMetadata.SvarPaMelding.ToString()) },
                {ReceivedMessageParser.EgendefinertHeaderPrefix + "test", Encoding.UTF8.GetBytes("Test")}
            };

            var propertiesMock = new Mock<IReadOnlyBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            IMottattMelding actualMelding = new MottattMelding(
                true,
                _fixture.DefaultMetadata,
                () => Task.FromResult((Stream)new MemoryStream(new byte[1])),
                Mock.Of<IAsicDecrypter>(),
                Mock.Of<IFileWriter>());
            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                actualMelding = messageArgs.Melding;
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                expectedMessageMetadata.MottakerKontoId.ToString(),
                propertiesMock.Object,
                Array.Empty<byte>());

            actualMelding.MeldingId.ShouldBe(expectedMessageMetadata.MeldingId);
            actualMelding.MeldingType.ShouldBe(expectedMessageMetadata.MeldingType);
            actualMelding.MottakerKontoId.ShouldBe(expectedMessageMetadata.MottakerKontoId);
            actualMelding.AvsenderKontoId.ShouldBe(expectedMessageMetadata.AvsenderKontoId);
            actualMelding.SvarPaMelding.ShouldBe(expectedMessageMetadata.SvarPaMelding);
            actualMelding.Ttl.ShouldBe(expectedMessageMetadata.Ttl);
            actualMelding.Headere["test"].ToString().ShouldBe("Test");
            actualMelding.Resendt.ShouldBeFalse();
        }

        [Fact]
        public async Task ReceivesExpectedMessageMetadataWithRedeliveredTrue()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.AvsenderKontoId.ToString()) },
                {"melding-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingId.ToString()) },
                {"type", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingType) },
                {"svar-til", Encoding.UTF8.GetBytes(expectedMessageMetadata.SvarPaMelding.ToString()) },
                {ReceivedMessageParser.EgendefinertHeaderPrefix + "test", Encoding.UTF8.GetBytes("Test")}
            };

            var propertiesMock = new Mock<IReadOnlyBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            IMottattMelding actualMelding = new MottattMelding(
                true,
                _fixture.DefaultMetadata,
                () => Task.FromResult((Stream)new MemoryStream(new byte[1])),
                Mock.Of<IAsicDecrypter>(),
                Mock.Of<IFileWriter>());
            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                actualMelding = messageArgs.Melding;
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                true,
                "exchange",
                expectedMessageMetadata.MottakerKontoId.ToString(),
                propertiesMock.Object,
                Array.Empty<byte>());

            actualMelding.MeldingId.ShouldBe(expectedMessageMetadata.MeldingId);
            actualMelding.MeldingType.ShouldBe(expectedMessageMetadata.MeldingType);
            actualMelding.MottakerKontoId.ShouldBe(expectedMessageMetadata.MottakerKontoId);
            actualMelding.AvsenderKontoId.ShouldBe(expectedMessageMetadata.AvsenderKontoId);
            actualMelding.SvarPaMelding.ShouldBe(expectedMessageMetadata.SvarPaMelding);
            actualMelding.Ttl.ShouldBe(expectedMessageMetadata.Ttl);
            actualMelding.Headere["test"].ToString().ShouldBe("Test");
            actualMelding.Resendt.ShouldBeTrue();
        }

        [Fact]
        public async Task ThrowsParseExceptionIfMessageIdIsNotValidGuid()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.AvsenderKontoId.ToString()) },
                {"melding-id", Encoding.UTF8.GetBytes("NoTGuid") },
                {"type", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingType) },
                {"svar-til", Encoding.UTF8.GetBytes(expectedMessageMetadata.SvarPaMelding.ToString()) }
            };

            var propertiesMock = new Mock<IReadOnlyBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) => { });

            sut.Received += handler;
            await Assert.ThrowsAsync<FiksIOParseException>(async () =>
            {
                await sut.HandleBasicDeliverAsync(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.MottakerKontoId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public async Task ThrowsMissingHeaderExceptionExceptionIfHeaderIsNull()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns((IDictionary<string, object>)null);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) => { });

            sut.Received += handler;
            await Assert.ThrowsAsync<FiksIOMissingHeaderException>(async () =>
            {
                await sut.HandleBasicDeliverAsync(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.MottakerKontoId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public async Task ThrowsMissingHeaderExceptionExceptionIfMessageIdIsMissing()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", Encoding.UTF8.GetBytes(expectedMessageMetadata.AvsenderKontoId.ToString()) },
                {"type", Encoding.UTF8.GetBytes(expectedMessageMetadata.MeldingType) },
                {"svar-til", Encoding.UTF8.GetBytes(expectedMessageMetadata.SvarPaMelding.ToString()) }
            };

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) => { });

            sut.Received += handler;
            await Assert.ThrowsAsync<FiksIOMissingHeaderException>(async () =>
            {
                await sut.HandleBasicDeliverAsync(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.MottakerKontoId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public async Task FileWriterWriteIsCalledWhenWriteEncryptedZip()
        {
            var sut = _fixture.CreateSut();

            var filePath = "/my/path/something.zip";
            var data = new[] {default(byte) };

            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                messageArgs.Melding.WriteEncryptedZip(filePath);
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.FileWriterMock.Verify(_ => _.Write(It.IsAny<Stream>(), filePath));
        }

        [Fact]
        public async Task DokumentlagerHandlerIsUsedWhenHeaderIsSet()
        {
            var sut = _fixture.WithDokumentlagerHeader().CreateSut();

            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                var stream = messageArgs.Melding.EncryptedStream;
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                null);

            _fixture.DokumentlagerHandler.Verify(_ => _.Download(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task DokumentlagerHandlerIsNotUsedWhenHeaderIsNotSet()
        {
            var sut = _fixture.WithoutDokumentlagerHeader().CreateSut();

            var data = new[] {default(byte) };

            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                var stream = messageArgs.Melding.EncryptedStream;
            });

            sut.Received += handler;
            
            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.DokumentlagerHandler.Verify(_ => _.Download(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task DataAsStreamIsReturnedWhenGettingEncryptedStream()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};

            Stream actualDataStream = new MemoryStream();
            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                actualDataStream = await messageArgs.Melding.EncryptedStream.ConfigureAwait(false);
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            var actualData = new byte[2];
            await actualDataStream.ReadAsync(actualData, 0, 2).ConfigureAwait(false);

            actualData[0].ShouldBe(data[0]);
            actualData[1].ShouldBe(data[1]);
        }

        [Fact]
        public async Task PayloadDecrypterDecryptIsCalledWhenGettingDecryptedStream()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};

            Stream actualDataStream = new MemoryStream();
            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                actualDataStream = await messageArgs.Melding.DecryptedStream.ConfigureAwait(false);
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.AsicDecrypterMock.Verify(_ => _.Decrypt(It.IsAny<Task<Stream>>()));

            var actualData = new byte[2];
            await actualDataStream.ReadAsync(actualData, 0, 2).ConfigureAwait(false);
            actualData[0].ShouldBe(data[0]);
            actualData[1].ShouldBe(data[1]);
        }

        [Fact]
        public async Task PayloadDecrypterAndFileWriterIsCalledWhenWriteDecryptedFile()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};
            var filePath = "/my/path/something.zip";

            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) =>
            {
                messageArgs.Melding.WriteDecryptedZip(filePath);
            });

            sut.Received += handler;

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.AsicDecrypterMock.Verify(_ => _.WriteDecrypted(It.IsAny<Task<Stream>>(), filePath));
        }

        [Fact]
        public async Task BasicAckIsCalledFromReplySender()
        {
            var sut = _fixture.CreateSut();
            var data = new[] {default(byte), byte.MaxValue};

            var handler = new EventHandler<MottattMeldingArgs>((a, messageArgs) => { messageArgs.SvarSender.Ack(); });
            var deliveryTag = (ulong)3423423;

            sut.Received += handler;
            await sut.HandleBasicDeliverAsync(
                "tag",
                deliveryTag,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.ModelMock.Verify(_ => _.BasicAckAsync(deliveryTag, false, CancellationToken.None));
        }

        [Fact]
        public async Task BasicAckIsNotCalledWithDeliveryTagIfReceiverIsNotSet()
        {
            var sut = _fixture.CreateSut();
            var data = new[] {default(byte), byte.MaxValue};

            await sut.HandleBasicDeliverAsync(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.ModelMock.Verify(_ => _.BasicAckAsync(It.IsAny<ulong>(), false, CancellationToken.None), Times.Never);
        }

        [Fact]
        public async Task ThrowsExceptionWhenGettingEncryptedStreamWithNoData()
        {
            var sut = _fixture.CreateSut();
            var data = Array.Empty<byte>();
            var correctExceptionThrown = false;

            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                try
                {
                    var stream = await messageArgs.Melding.EncryptedStream;
                }
                catch (FiksIOMissingDataException ex)
                {
                    correctExceptionThrown = true;
                }
            });
            var deliveryTag = (ulong) 3423423;

            sut.Received += handler;
            await sut.HandleBasicDeliverAsync(
                "tag",
                deliveryTag,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            correctExceptionThrown.ShouldBeTrue();
        }

        [Fact]
        public async Task ThrowsExceptionWhenGettingDecryptedStreamWithNoData()
        {
            var sut = _fixture.CreateSut();
            var data = Array.Empty<byte>();
            var correctExceptionThrown = false;

            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                try
                {
                    var stream = await messageArgs.Melding.DecryptedStream;
                }
                catch (FiksIOMissingDataException ex)
                {
                    correctExceptionThrown = true;
                }
            });
            var deliveryTag = (ulong) 3423423;

            sut.Received += handler;
            await sut.HandleBasicDeliverAsync(
                "tag",
                deliveryTag,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            correctExceptionThrown.ShouldBeTrue();
        }

        [Fact]
        public async Task ThrowsExceptionWhenWritingDecryptedStreamWithNoData()
        {
            var sut = _fixture.CreateSut();
            var data = Array.Empty<byte>();
            var correctExceptionThrown = false;

            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                try
                {
                    await messageArgs.Melding.WriteDecryptedZip("out.zip").ConfigureAwait(false);
                }
                catch (FiksIOMissingDataException ex)
                {
                    correctExceptionThrown = true;
                }
            });
            var deliveryTag = (ulong) 3423423;

            sut.Received += handler;
            await sut.HandleBasicDeliverAsync(
                "tag",
                deliveryTag,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            correctExceptionThrown.ShouldBeTrue();
        }

        [Fact]
        public async Task ThrowsExceptionWhenWritingEnryptedStreamWithNoData()
        {
            var sut = _fixture.CreateSut();
            var data = Array.Empty<byte>();
            var correctExceptionThrown = false;

            var handler = new EventHandler<MottattMeldingArgs>(async (a, messageArgs) =>
            {
                try
                {
                    await messageArgs.Melding.WriteEncryptedZip("out.zip").ConfigureAwait(false);
                }
                catch (FiksIOMissingDataException ex)
                {
                    correctExceptionThrown = true;
                }
            });
            var deliveryTag = (ulong) 3423423;

            sut.Received += handler;
            await sut.HandleBasicDeliverAsync(
                "tag",
                deliveryTag,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            correctExceptionThrown.ShouldBeTrue();
        }
    }
}