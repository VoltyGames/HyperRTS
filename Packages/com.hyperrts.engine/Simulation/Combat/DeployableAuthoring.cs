using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A unit that deploys to fire and packs up to move, through the Deploy and Pack Up commands. Its weapons marked
    /// Needs Deploy hold fire until it is deployed, and it refuses moves until it has packed up.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Deployable")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class DeployableAuthoring : AuthoringBehaviour
    {
        [Tooltip("Seconds from the Deploy command until its weapons may fire.")]
        [Min(0.1f)]
        public float deployTime = 4f;

        [Tooltip("Seconds from the Pack Up command until it may move.")]
        [Min(0.1f)]
        public float packUpTime = 3f;

        public static void Add<TWriter>(ref TWriter writer, float deployTime, float packUpTime)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Deployable { DeployTime = deployTime, PackUpTime = packUpTime });
        }

        public class Baker : Baker<DeployableAuthoring>
        {
            public override void Bake(DeployableAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, authoring.deployTime, authoring.packUpTime);
            }
        }
    }
}
