using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>
    /// One hit waiting for <see cref="DamageSystem"/>: full damage to <see cref="Target"/>, plus falloff splash around
    /// <see cref="Position"/> when <see cref="Radius"/> is set. A negative amount heals.
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
        public byte SourceFaction;
        public float Amount;
        public UnityObjectRef<DamageType> Type;

        /// <summary>The <see cref="DamageType.garrisonFactor"/> of <see cref="Type"/>; 0 never reaches a garrison.</summary>
        public float GarrisonFactor;

        /// <summary>The <see cref="DamageType.coverPiercing"/> of <see cref="Type"/>.</summary>
        public float CoverPiercing;

        /// <summary>Suppression a hit adds to a squad it lands on, before cover; see <see cref="Suppression"/>.</summary>
        public float Suppression;

        public float Radius;

        /// <summary>Fraction of <see cref="Amount"/> dealt at the splash edge, 1 = no falloff.</summary>
        public float EdgeFactor;

        /// <summary>Splash also hurts the source's allies.</summary>
        public bool FriendlyFire;

        /// <summary>Layers the splash reaches: the weapon's targets, else the surface only.</summary>
        public WeaponTargets Reach;

        /// <summary>A plain hit or heal on <paramref name="target"/> alone, untyped and without splash.</summary>
        public static DamageEvent Direct(Entity target, float3 position, Entity source, byte faction,
            float amount) => new()
        {
            Target = target,
            Position = position,
            Origin = position,
            Source = source,
            SourceFaction = faction,
            Amount = amount,
            EdgeFactor = 1f,
        };

        /// <summary>This hit with a damage type, such as one tick of damage over time; no garrison share.</summary>
        public readonly DamageEvent Typed(UnityObjectRef<DamageType> type)
        {
            var hit = this;
            hit.Type = type;
            return hit;
        }

        /// <summary>This hit coming from <paramref name="origin"/>, for directional armor.</summary>
        public readonly DamageEvent From(float3 origin)
        {
            var hit = this;
            hit.Origin = origin;
            return hit;
        }

        /// <summary>Linear falloff from full damage at the centre to <see cref="EdgeFactor"/> at <see cref="Radius"/>.</summary>
        public readonly float SplashFactor(float gap) =>
            math.lerp(1f, EdgeFactor, math.saturate(gap / math.max(Radius, 1e-4f)));

        /// <summary>Reports where a shot of a shooter type landed; a type id of 0 is ignored.</summary>
        public readonly void AddImpact(ref CueWriter cues, int typeId, float3 direction) =>
            cues.Add(new CueEvent
            {
                TypeId = typeId, Slot = CueSlot.Impact, Faction = SourceFaction, Position = Position,
                Direction = direction, Radius = Radius, Target = Target,
            });
    }

    /// <summary>Tags the singleton holding this frame's <see cref="DamageEvent"/>s.</summary>
    public struct DamageQueue : IComponentData { }
}
