using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Death
{
    /// <summary>
    /// A unit that throws its turret when a catastrophic hit kills it: a lethal <see cref="TossType"/> or overkill
    /// past <see cref="Overkill"/> of its maximum health. The wreck is then <see cref="Hull"/> and the turret lands as
    /// <see cref="Turret"/> within <see cref="Arc"/> radians of the way the hit pushed, <see cref="MinDistance"/> to
    /// <see cref="MaxDistance"/> metres away.
    /// </summary>
    public struct TurretToss : IComponentData
    {
        public Entity Hull;
        public Entity Turret;
        public float Overkill;
        public float MinDistance;
        public float MaxDistance;
        public float Arc;
        public float Height;
        public float Duration;

        /// <summary>True when the lethal hit was a <see cref="TossType"/> or overshot the unit's health by enough.</summary>
        public readonly bool IsCatastrophic(in LethalHit hit, DynamicBuffer<TossType> types, float maxHealth)
        {
            if (hit.Overkill >= Overkill * maxHealth)
            {
                return true;
            }

            foreach (var type in types)
            {
                if (type.DamageType.Equals(hit.Type))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The toss from <paramref name="from"/> to a seeded landing point, fixed by where the unit died and how it
        /// was hit, so every run lands it in the same place.
        /// </summary>
        public readonly TossedTurret Throw(float3 from, byte heading, out float3 landing)
        {
            var random = Random.CreateFromIndex(math.hash(new uint3(math.asuint(from.x), math.asuint(from.z), heading)));
            var turn = (random.NextFloat() - 0.5f) * Arc;
            var away = math.rotate(quaternion.RotateY(turn), HeadingByte.Direction(heading));
            landing = from + away * random.NextFloat(MinDistance, MaxDistance);
            var turns = random.NextFloat(1f, 2f) * (random.NextBool() ? 1f : -1f);
            return new TossedTurret
            {
                From = from, Height = Height, Duration = Duration, Spin = turns * 2f * math.PI / Duration,
            };
        }
    }

    /// <summary>A damage type that makes a lethal hit catastrophic for a <see cref="TurretToss"/> unit.</summary>
    [InternalBufferCapacity(0)]
    public struct TossType : IBufferElementData
    {
        public UnityObjectRef<DamageType> DamageType;
    }

    /// <summary>The hit that killed a <see cref="TurretToss"/> unit or a <see cref="Topple"/> piece: its type, how far it overshot and which way it pushed.</summary>
    public struct LethalHit : IComponentData
    {
        public UnityObjectRef<DamageType> Type;
        public float Overkill;
        public byte Heading;
    }

    /// <summary>
    /// Remains thrown from a hull: presentation flies them from <see cref="From"/> to where they lie, spinning at
    /// <see cref="Spin"/> radians a second, in <see cref="Duration"/> seconds.
    /// </summary>
    public struct TossedTurret : IComponentData
    {
        [GhostField(Quantization = 100)] public float3 From;
        [GhostField(Quantization = 100)] public float Height;
        [GhostField(Quantization = 100)] public float Duration;
        [GhostField(Quantization = 100)] public float Spin;
    }
}
