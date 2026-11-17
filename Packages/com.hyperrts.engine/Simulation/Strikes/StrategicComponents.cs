using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Combat.Damage;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>A stick of guided bombs that seek enemy structures in a zone round the aim point.</summary>
    public struct BombLoad
    {
        /// <summary>Projectile prefab each bomb flies as.</summary>
        public Entity Prefab;

        public int Count;

        /// <summary>Each bomb's hit; its splash reaches the surface only.</summary>
        public DamageProfile Damage;

        public float Speed;

        /// <summary>Radius round the aim point whose enemy structures the bombs seek.</summary>
        public float ZoneRadius;

        /// <summary>Share of <see cref="Count"/> that enemy air defence over the zone can remove at most.</summary>
        public float MaxReduction;

        /// <summary>Prefab each bomb leaves where it lands (<see cref="StrikeRemains"/>); null leaves nothing.</summary>
        public Entity Remains;

        /// <summary>Bombs left once each of <paramref name="defences"/> removes one, never below the cap.</summary>

        public readonly int CountAfter(int defences) => Count - math.min(defences, (int)(Count * MaxReduction));
    }

    /// <summary>
    /// A structure's bomber strike: firing the ability with <see cref="AbilityId"/> on a point sends
    /// <see cref="Aircraft"/> in from the owner's map edge to drop <see cref="Bombs"/> there.
    /// </summary>
    public struct BomberStrike : IComponentData
    {
        public int AbilityId;
        public Entity Aircraft;

        /// <summary>Cruise height of the aircraft above the target.</summary>
        public float Altitude;

        /// <summary>Seconds before the drop at which the aircraft loses its stealth and the strike warning shows.</summary>
        public float RevealSeconds;

        public BombLoad Bombs;
    }

    /// <summary>
    /// A structure's missile strike: firing the ability with <see cref="AbilityId"/> on a point launches
    /// <see cref="Missile"/> from <see cref="LaunchPoint"/>, which climbs, turns over and lands
    /// <see cref="FlightSeconds"/> later. Interceptor kill rolls never reach it; enemy defences shrink its blast.
    /// </summary>
    public struct MissileStrike : IComponentData
    {
        public int AbilityId;

        /// <summary>Projectile prefab the missile flies as.</summary>
        public Entity Missile;

        /// <summary>Where the missile leaves, in the structure's space.</summary>
        public float3 LaunchPoint;

        /// <summary>Height of the climb above the launch and the dive above the target.</summary>
        public float Apex;

        public float FlightSeconds;

        /// <summary>The blast on landing; its splash reaches the surface only.</summary>
        public DamageProfile Blast;

        /// <summary>Prefab the missile leaves where it lands (<see cref="StrikeRemains"/>); null leaves nothing.</summary>
        public Entity Remains;

        /// <summary>Share of the blast's radii each enemy defence covering the target cuts.</summary>
        public float CutPerDefence;

        /// <summary>Share of the blast's radii enemy defences cut at most.</summary>
        public float MaxCut;

        /// <summary>The blast once <paramref name="defences"/> cover the target: smaller, never weaker.</summary>
        public readonly DamageProfile BlastAfter(int defences)
        {
            var scale = 1f - math.min(MaxCut, defences * CutPerDefence);
            var blast = Blast;
            blast.SplashRadius *= scale;
            blast.CoreRadius *= scale;
            return blast;
        }
    }

    /// <summary>
    /// Makes one of a structure's strike abilities its strategic asset: the ability's cooldown is the map-wide charge
    /// every player sees.
    /// </summary>
    public struct StrategicAsset : IComponentData
    {
        public int AbilityId;

        /// <summary>The owner the charge belongs to; a new owner starts it over.</summary>
        public byte Owner;
    }

    /// <summary>A <see cref="DropFlight"/> on a bomber strike; shot down before the drop, the strike is lost.</summary>
    public struct BombingFlight : IComponentData
    {
        public float RevealSeconds;
        public BombLoad Bombs;

        /// <summary>The caster's type when it has an impact cue, played where each bomb lands (0 = none).</summary>
        public int ImpactTypeId;

        /// <summary>The caster's type when it has a warning cue (0 = none).</summary>
        public int WarningTypeId;

        /// <summary>The strike warning has shown.</summary>
        public bool Warned;
    }

    /// <summary>
    /// What a strike's projectile leaves where it lands, such as a burning crater whose smoke hangs over the ground;
    /// shot down, it leaves nothing.
    /// </summary>
    public struct StrikeRemains : IComponentData
    {
        public Entity Prefab;
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
