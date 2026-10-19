using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Client singleton: this process's identity, sent with every join so a reconnect gets its old slot back, seeded
    /// from <c>NetworkSession.Token</c>.
    /// </summary>
    public struct JoinToken : IComponentData
    {
        public ulong Value;
    }
}
