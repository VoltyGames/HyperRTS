using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Singleton: the player entity of each faction, rebuilt at the start of every frame by
    /// <c>PlayerRegistrySystem</c>. A value type, so jobs take a copy.
    /// </summary>
    public struct PlayerRegistry : IComponentData
    {
        /// <summary>One slot per faction, neutral included; size per-faction arrays with it.</summary>
        public const int Capacity = Faction.Max + 1;

        private FixedList512Bytes<Entity> _byFaction;

        /// <summary>The faction's player entity; <c>Entity.Null</c> when it has none.</summary>
        public readonly Entity this[byte faction] => faction < _byFaction.Length ? _byFaction[faction] : Entity.Null;

        public void Set(byte faction, Entity player)
        {
            while (_byFaction.Length <= faction)
            {
                _byFaction.Add(Entity.Null);
            }

            _byFaction[faction] = player;
        }
    }
}
