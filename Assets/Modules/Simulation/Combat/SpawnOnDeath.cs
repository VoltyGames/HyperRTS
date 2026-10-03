using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Prefab instantiated where the entity dies (wreck, debris, explosion).</summary>
    public struct SpawnOnDeath : IComponentData
    {
        public Entity Prefab;
    }
}
