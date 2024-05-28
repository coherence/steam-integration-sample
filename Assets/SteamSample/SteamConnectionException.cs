using Coherence.Connection;
using Steamworks;
using Steamworks.Data;

namespace SteamSample
{
    public class SteamConnectionException : ConnectionException
    {
        public ConnectionInfo ConnectionInfo;

        public SteamConnectionException(ConnectionInfo connectionInfo) : base(GetMessage(connectionInfo))
        {
            ConnectionInfo = connectionInfo;
        }

        private static string GetMessage(ConnectionInfo info)
        {
            return $"Peer disconnected: {info.State} " +
                   $"EndReason: {GetEndReasonString(info)} ({(int)info.EndReason}) " +
                   $"Address: {info.Address} " +
                   $"Identity: {info.Identity}";
        }

        public static string GetEndReasonString(ConnectionInfo info)
        {
            return info.EndReason switch
            {
                NetConnectionEnd.App_Min => "Host Disconnected",
                NetConnectionEnd.Misc_Generic => "Generic Connection Error",
                NetConnectionEnd.Misc_InternalError => "Internal Timeout",
                NetConnectionEnd.Misc_Timeout => "Timeout",
                _ => info.EndReason.ToString()
            };
        }
    }
}