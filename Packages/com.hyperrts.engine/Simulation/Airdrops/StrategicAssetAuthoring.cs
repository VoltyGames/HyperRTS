using HyperRTS.Core;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// Turns one of this structure's abilities into a strategic asset: the ability's cooldown is the map-wide charge,
    /// and firing it sends a bomber in from the owner's map edge to drop guided bombs on the target zone.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Strategic Asset")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.StrategicAssets)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    [RequiresAuthoring(typeof(AbilityAuthoring), "an Ability")]
    public class StrategicAssetAuthoring : AuthoringBehaviour
    {
        [Tooltip("Name of the point-targeted Ability entry that charges and fires the strike; it starts on cooldown.")]
        public string ability = "Strategic Strike";

        [Tooltip("Aircraft prefab with Flight and Stealth that flies the strike; air defence can engage it once revealed.")]
        public UnitAuthoring aircraft;

        [Tooltip("Cruise height of the aircraft above the target.")]
        [Min(1f)]
        public float altitude = 60f;

        [Tooltip("Seconds before the drop at which the aircraft becomes detectable.")]
        [Min(0f)]
        public float revealSeconds = 5f;

        [Header("Bombs")]
        [Tooltip("Projectile prefab each guided bomb flies as.")]
        public GameObject bomb;

        [Tooltip("Bombs in the stick before air defence removes any.")]
        [Min(1)]
        public int count = 8;

        [Tooltip("Damage each bomb deals at its centre.")]
        [Min(0f)]
        public float damage = 800f;

        [Tooltip("Splash radius of each bomb.")]
        [Min(0f)]
        public float radius = 6f;

        [Tooltip("Share of the damage dealt at the splash edge.")]
        [Range(0f, 1f)]
        public float edgeFactor = 0.5f;

        [Tooltip("Damage type that armor scales.")]
        public DamageType damageType;

        [Tooltip("Metres per second each bomb falls toward its aim.")]
        [Min(1f)]
        public float speed = 40f;

        [Tooltip("Radius round the aim point whose enemy structures the bombs seek.")]
        [Min(0f)]
        public float zoneRadius = 25f;

        [Tooltip("Share of the bombs enemy air defence over the zone can remove at most, one bomb per weapon.")]
        [Range(0f, 1f)]
        public float maxReduction = 0.5f;

        public class Baker : Baker<StrategicAssetAuthoring>
        {
            public override void Bake(StrategicAssetAuthoring authoring)
            {
                // StrategicAssetRule reports a missing aircraft, bomb or ability entry.
                if (authoring.aircraft == null || authoring.bomb == null)
                {
                    return;
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new StrategicAsset
                {
                    AbilityId = EntityInfo.TypeIdFromName(authoring.ability),
                    Aircraft = GetEntity(authoring.aircraft, TransformUsageFlags.Dynamic),
                    Altitude = authoring.altitude,
                    RevealSeconds = authoring.revealSeconds,
                    Bombs = new BombLoad
                    {
                        Prefab = GetEntity(authoring.bomb, TransformUsageFlags.Dynamic),
                        Count = authoring.count,
                        Damage = authoring.damage,
                        Radius = authoring.radius,
                        EdgeFactor = authoring.edgeFactor,
                        DamageType = authoring.damageType,
                        Speed = authoring.speed,
                        ZoneRadius = authoring.zoneRadius,
                        MaxReduction = authoring.maxReduction,
                    },
                });
            }
        }
    }
}
