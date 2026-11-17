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
    /// Turns one of this structure's abilities into a missile strike: firing it launches a missile from the structure
    /// that lands on the target a set time later, with a warning every player sees for the whole flight.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Combat/Missile Strike")]
    [Icon(HyperRTSIcons.MissileStrikeAuthoring)]
    [HelpURL(HyperRTSDocs.StrategicAssets)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    [RequiresAuthoring(typeof(AbilityAuthoring), "an Ability")]
    public class MissileStrikeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Name of the point-targeted Ability entry that fires the strike; it starts on cooldown.")]
        public string ability = "Strategic Strike";

        [Tooltip("The ability's cooldown is the owner's strategic asset charge, shown to every player.")]
        public bool strategic = true;

        [Tooltip("Projectile prefab the missile flies as.")]
        [PrefabAsset("Missile")]
        public GameObject missile;

        [Tooltip("Child transform the missile leaves from, such as the silo mouth; none launches from the root.")]
        public Transform launchPoint;

        [Tooltip("Height of the climb above the launch point and of the dive above the target.")]
        [Unit("m")]
        [Min(1f)]
        public float apex = 300f;

        [Tooltip("Seconds from launch to landing, which is how long the warning shows.")]
        [Unit("s")]
        [Min(1f)]
        public float flightSeconds = 10f;

        [Tooltip("The blast where it lands: full damage inside the core radius, falling off to the splash radius.")]
        public DamageSettings blast = new() { damage = 5000f, splashRadius = 120f, coreRadius = 40f, edgeFactor = 0f };

        [Tooltip("Share of the blast's radii each enemy anti-air or missile interceptor covering the target cuts.")]
        [Range(0f, 1f)]
        public float cutPerDefence = 0.1f;

        [Tooltip("Share of the blast's radii enemy defences cut at most.")]
        [Range(0f, 1f)]
        public float maxCut = 0.5f;

        public class Baker : Baker<MissileStrikeAuthoring>
        {
            public override void Bake(MissileStrikeAuthoring authoring)
            {
                // StrikeRule reports a missing missile or ability entry.
                if (authoring.missile == null)
                {
                    return;
                }

                var writer = this.Writer();
                var id = EntityInfo.TypeIdFromName(authoring.ability);
                var root = authoring.transform;
                var launch = authoring.launchPoint == null ? root : authoring.launchPoint;
                DependsOn(launch);
                writer.Add(new MissileStrike
                {
                    AbilityId = id,
                    Missile = GetEntity(authoring.missile, TransformUsageFlags.Dynamic),
                    LaunchPoint = root.InverseTransformPoint(launch.position),
                    Apex = authoring.apex,
                    FlightSeconds = authoring.flightSeconds,
                    Blast = authoring.blast.Bake(),
                    CutPerDefence = authoring.cutPerDefence,
                    MaxCut = authoring.maxCut,
                });
                if (authoring.strategic)
                {
                    writer.Add(new StrategicAsset { AbilityId = id });
                }
            }
        }
    }
}
