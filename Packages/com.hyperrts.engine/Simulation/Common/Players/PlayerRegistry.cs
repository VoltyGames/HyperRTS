using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Players
{
    /// <summary>
    /// Singleton: the player entity of each owner, rebuilt at the start of every frame by
    /// <c>PlayerRegistrySystem</c>. A value type, so jobs take a copy.
    /// </summary>
    public struct PlayerRegistry : IComponentData
    {
        /// <summary>One slot per owner, neutral included; size per-owner arrays with it.</summary>
        public const int Capacity = Owner.Max + 1;

        private FixedList512Bytes<Entity> _byOwner;

        /// <summary>The owner's player entity; <c>Entity.Null</c> when it has none.</summary>
        public readonly Entity this[byte owner] => owner < _byOwner.Length ? _byOwner[owner] : Entity.Null;

        public void Set(byte owner, Entity player)
        {
            while (_byOwner.Length <= owner)
            {
                _byOwner.Add(Entity.Null);
            }

            _byOwner[owner] = player;
        }
    }
}
