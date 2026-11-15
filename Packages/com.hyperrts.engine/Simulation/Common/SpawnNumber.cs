using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// A unit's or building's number, unique in its match: the authoritative world gives it once, from 1, and clients
    /// and replays read the same one. 0 until given, and for cues with no source.
    /// </summary>
    public struct SpawnNumber : IComponentData
    {
        [GhostField] public int Value;
    }
}
