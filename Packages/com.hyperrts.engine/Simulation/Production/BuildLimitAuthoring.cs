using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Caps how many of this unit or building each player may have (a superweapon, a hero unit).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Build Limit")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Production)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class BuildLimitAuthoring : AuthoringBehaviour
    {
        [Tooltip("Most a player may have at once, counting standing, under construction and queued.")]
        [Min(1)]
        public int max = 1;

        public class Baker : Baker<BuildLimitAuthoring>
        {
            public override void Bake(BuildLimitAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                BuildLimitSetup.Add(ref writer, authoring.max);
            }
        }
    }
}
