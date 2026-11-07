using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>
    /// One hit waiting for <see cref="DamageSystem"/>: full damage to <see cref="Target"/>, plus falloff splash around
    /// <see cref="Position"/> when the profile has a radius. A negative amount heals.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DamageEvent : IBufferElementData
    {
        /// <summary>Takes full damage; <c>Entity.Null</c> for a pure area hit.</summary>
        public Entity Target;

        public float3 Position;

        /// <summary>Where the hit came from; directional armor compares it with the target's facing.</summary>
        public float3 Origin;

        public Entity Source;
        public byte SourceOwner;

        /// <summary>What the hit deals: amount, damage type, cover piercing, suppression and splash.</summary>
        public DamageProfile Profile;

        /// <summary>Layers the splash reaches: the weapon's targets.</summary>
        public WeaponTargets Layers;

        /// <summary>A plain hit or heal on <paramref name="target"/> alone, untyped and without splash.</summary>
        public static DamageEvent Direct(Entity target, float3 position, Entity source, byte owner,
            float amount) => new()
        {
            Target = target,
            Position = position,
            Origin = position,
            Source = source,
            SourceOwner = owner,
            Profile = new DamageProfile { Amount = amount, EdgeFactor = 1f },
        };

        /// <summary>This hit with the damage type and cover piercing of <paramref name="kind"/>, such as one tick of damage over time.</summary>
        public readonly DamageEvent Typed(in DamageProfile kind)
        {
            var hit = this;
            hit.Profile.Type = kind.Type;
            hit.Profile.CoverPiercing = kind.CoverPiercing;
            return hit;
        }

        /// <summary>This hit coming from <paramref name="origin"/>, for directional armor.</summary>
        public readonly DamageEvent From(float3 origin)
        {
            var hit = this;
            hit.Origin = origin;
            return hit;
        }

        /// <summary>Reports where a shot of a shooter type landed; a type id of 0 is ignored.</summary>
        public readonly void AddImpact(ref CueWriter cues, int typeId, float3 direction) =>
            cues.Add(new CueEvent
            {
                TypeId = typeId, Slot = CueSlot.Impact, Owner = SourceOwner, Position = Position,
                Direction = direction, Radius = Profile.Radius, Target = Target,
            });
    }

    /// <summary>Tags the singleton holding this frame's <see cref="DamageEvent"/>s.</summary>
    public struct DamageQueue : IComponentData { }
}
