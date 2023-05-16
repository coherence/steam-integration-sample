using Coherence.Log;
using Coherence.Stats;
using Coherence.Transport;
using Steamworks;

namespace SteamSample
{
    public class SteamTransportFactory : ITransportFactory
    {
        private readonly SteamId hostSteamId;

        public SteamTransportFactory(SteamId hostSteamId)
        {
            this.hostSteamId = hostSteamId;
        }

        public ITransport Create(IStats stats, Logger logger)
        {
            return new SteamTransport(stats, logger)
            {
                HostSteamId = hostSteamId
            };
        }
    }
}