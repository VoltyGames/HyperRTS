using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Status
{
    /// <summary>
    /// A status effect on this entity, kept by <see cref="StatusEffectSystem"/> on the server. Clients get only the
    /// type id and end time for the HUD, and find the effect's name and icon through the prefab registry.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ActiveStatus : IBufferElementData
    {
        [GhostField] public int TypeId;

        /// <summary>Elapsed world time the effect ends at.</summary>
        [GhostField] public float EndsAt;

        [GhostField(SendData = false)] public Entity Definition;

        /// <summary>Who applied it last; damage over time credits them.</summary>
        [GhostField(SendData = false)] public Entity Source;

        [GhostField(SendData = false)] public byte SourceFaction;
        [GhostField(SendData = false)] public int Stacks;
    }
}
