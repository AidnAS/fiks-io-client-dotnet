using System;
using System.Threading;
using System.Threading.Tasks;
using KS.Fiks.IO.Client.Configuration;
using KS.Fiks.IO.Send.Client.Configuration;
using Ks.Fiks.Maskinporten.Client;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace KS.Fiks.IO.Client.Amqp
{
    public class MaskinportenCredentialsProvider : ICredentialsProvider
    {
        private const int TokenRetrievalTimeout = 5000;
        private static ILogger<MaskinportenCredentialsProvider> _logger;
        private readonly IMaskinportenClient _maskinportenClient;
        private readonly IntegrasjonConfiguration _integrasjonConfiguration;
        private ReaderWriterLock _lock = new ReaderWriterLock();
        private MaskinportenToken _maskinportenToken;

        public MaskinportenCredentialsProvider(string name, IMaskinportenClient maskinportenClient, IntegrasjonConfiguration integrasjonConfiguration, ILoggerFactory loggerFactory = null)
        {
            Name = name;
            _maskinportenClient = maskinportenClient;
            _integrasjonConfiguration = integrasjonConfiguration;
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<MaskinportenCredentialsProvider>();
            }
        }

        public async Task<Credentials> GetCredentialsAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var token = await CheckState();
            var password = $"{_integrasjonConfiguration.IntegrasjonPassord} {token.Token}";

            return new Credentials(Name, UserName, password, null);
        }

        public string Name { get; }

        public string UserName => _integrasjonConfiguration.IntegrasjonId.ToString();

        private async Task<MaskinportenToken> CheckState()
        {
            _lock.AcquireReaderLock(TokenRetrievalTimeout);
            try
            {
                if (_maskinportenToken != null && !_maskinportenToken.IsExpiring())
                {
                    return _maskinportenToken;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }

            return await RetrieveToken();
        }

        private async Task<MaskinportenToken> RetrieveToken()
        {
            _lock.AcquireWriterLock(TokenRetrievalTimeout);
            
            try
            {
                return await RequestOrRenewToken();
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
        }

        async Task<MaskinportenToken> RequestOrRenewToken()
        {
            _maskinportenToken = await _maskinportenClient.GetAccessToken(_integrasjonConfiguration.Scope);
            return _maskinportenToken;
        }
    }
}