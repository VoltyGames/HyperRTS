using HyperRTS.Simulation.Combat;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>A stick of guided bombs that seek enemy structures in a zone round the aim point.</summary>
    public struct BombLoad
    {
        /// <summary>Projectile prefab each bomb flies as.</summary>
        public Entity Prefab;

        public int Count;
        public float Damage;
        public float Radius;
        public float EdgeFactor;
        public UnityObjectRef<DamageType> DamageType;
        public float Speed;

        /// <summary>Radius round the aim point whose enemy structures the bombs seek.</summary>
        public float ZoneRadius;

        /// <summary>Share of <see cref="Count"/> that enemy air defence over the zone can remove at most.</summary>
        public float MaxReduction;

        /// <summary>Bombs left once each of <paramref name="defences"/> removes one, never below the cap.</summary>
        public readonly int CountAfter(int defences) => Count - math.min(defences, (int)(Count * MaxReduction));
    }

    /// <summary>
    /// A structure's strategic asset: its ability with <see cref="AbilityId"/> charges the strike, and firing it on a
    /// point sends <see cref="Aircraft"/> in from the owner's map edge to drop <see cref="Bombs"/> there.
    /// </summary>
    public struct StrategicAsset : IComponentData
    {
        public int AbilityId;
        public Entity Aircraft;

        /// <summary>Cruise height of the aircraft above the target.</summary>
        public float Altitude;

        /// <summary>Seconds before the drop at which the aircraft loses its stealth.</summary>
        public float RevealSeconds;

        public BombLoad Bombs;

        /// <summary>The faction the charge belongs to; a new owner starts it over.</summary>
        public byte Owner;
    }

    /// <summary>An aircraft on a strategic strike; shot down before the drop, the strike is lost.</summary>
    public struct BombingFlight : IComponentData
    {
        public DropRoute Route;
        public float RevealSeconds;
        public BombLoad Bombs;

        /// <summary>Set once it dropped; it then flies off the map and despawns.</summary>
        public bool Leaving;
    }

    /// <summary>A player's strategic asset charge, replicated to every player for the map-wide countdown.</summary>
    public struct StrategicCharge : IComponentData
    {
        /// <summary>Owns a charging or charged asset this frame.</summary>
        [GhostField] public bool HasAsset;

        /// <summary>Seconds to the charge, to a tenth of a second.</summary>
        [GhostField(Quantization = 10)] public float Remaining;

        [GhostField(Quantization = 10)] public float Duration;

        public readonly bool IsReady => HasAsset && Remaining <= 0f;
    }
}
