using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Turns one of this structure's abilities into a bomber strike: firing it sends a bomber in from the owner's map
    /// edge to drop guided bombs on the target zone.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Combat/Bomber Strike")]
    [Icon(HyperRTSIcons.BomberStrikeAuthoring)]
    [HelpURL(HyperRTSDocs.StrategicAssets)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    [RequiresAuthoring(typeof(AbilityAuthoring), "an Ability")]
    public class BomberStrikeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Name of the point-targeted Ability entry that fires the strike; it starts on cooldown.")]
        public string ability = "Strategic Strike";

        [Tooltip("The ability's cooldown is the owner's strategic asset charge, shown to every player.")]
        public bool strategic = true;

        [Tooltip("Aircraft prefab with Flight and Stealth that flies the strike; air defence can engage it once revealed.")]
        [PrefabAsset("Aircraft")]
        public UnitAuthoring aircraft;

        [Tooltip("Cruise height of the aircraft above the target.")]
        [Min(1f)]
        public float altitude = 60f;

        [Tooltip("Seconds before the drop at which the aircraft becomes detectable and the strike warning shows.")]
        [Min(0f)]
        public float revealSeconds = 5f;

        [Header("Bombs")]
        [Tooltip("Projectile prefab each guided bomb flies as.")]
        [PrefabAsset("Bomb")]
        public GameObject bomb;

        [Tooltip("Bombs in the stick before air defence removes any.")]
        [Min(1)]
        public int count = 8;

        [Tooltip("What each bomb deals at its centre and round it.")]
        public DamageSettings hit = new() { damage = 800f, splashRadius = 6f };

        [Tooltip("How fast each bomb falls toward its aim.")]
        [Unit("m/s")]
        [Min(1f)]
        public float speed = 40f;

        [Tooltip("Radius round the aim point whose enemy structures the bombs seek; 0 drops them all on the point.")]
        [Min(0f)]
        public float zoneRadius = 25f;

        [Tooltip("Share of the bombs enemy air defence over the zone can remove at most, one bomb per weapon.")]
        [Range(0f, 1f)]
        public float maxReduction = 0.5f;

        public class Baker : Baker<BomberStrikeAuthoring>
        {
            public override void Bake(BomberStrikeAuthoring authoring)
            {
                // StrikeRule reports a missing aircraft, bomb or ability entry.
                if (authoring.aircraft == null || authoring.bomb == null)
                {
                    return;
                }

                var writer = this.Writer();
                var id = EntityInfo.TypeIdFromName(authoring.ability);
                writer.Add(new BomberStrike
                {
                    AbilityId = id,
                    Aircraft = GetEntity(authoring.aircraft, TransformUsageFlags.Dynamic),
                    Altitude = authoring.altitude,
                    RevealSeconds = authoring.revealSeconds,
                    Bombs = new BombLoad
                    {
                        Prefab = GetEntity(authoring.bomb, TransformUsageFlags.Dynamic),
                        Count = authoring.count,
                        Damage = authoring.hit.Bake(),
                        Speed = authoring.speed,
                        ZoneRadius = authoring.zoneRadius,
                        MaxReduction = authoring.maxReduction,
                    },
                });
                if (authoring.strategic)
                {
                    writer.Add(new StrategicAsset { AbilityId = id });
                }
            }
        }
    }
}
