using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Blocks a box of the pathfinding grid (rocks, cliffs). Buildings block their footprint already; water and
    /// bridges are Nav Areas.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Nav Obstacle")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class NavObstacleAuthoring : AuthoringBehaviour
    {
        [Tooltip("Blocked area (X by Z) centred on this transform.")]
        public Vector2 size = new(2f, 2f);

        public class Baker : Baker<NavObstacleAuthoring>
        {
            public override void Bake(NavObstacleAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                NavSetup.AddObstacle(ref sink, authoring.size);
            }
        }
    }
}
