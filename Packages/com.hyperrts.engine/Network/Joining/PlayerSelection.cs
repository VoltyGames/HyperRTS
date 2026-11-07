using Unity.Entities;

namespace HyperRTS.Network.Joining
{
    /// <summary>
    /// Server only, on a remote player's entity: the units its client last selected, kept to those it owned on
    /// arrival. Commands without a unit of their own address it.
    /// </summary>
    public struct PlayerSelection : IBufferElementData
    {
        public Entity Value;
    }
}
