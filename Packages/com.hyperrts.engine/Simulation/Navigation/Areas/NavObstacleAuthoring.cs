using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation.Areas
{
    /// <summary>
    /// Blocks a box of the pathfinding grid (rocks, cliffs, a railing). Buildings block their footprint already; water
    /// and bridges are Nav Areas. On a child it keeps its place in the world, so it never moves with its parent.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Navigation/Nav Obstacle")]
    [Icon(HyperRTSIcons.NavObstacleAuthoring)]
    [HelpURL(HyperRTSDocs.Navigation)]
    [DisallowMultipleComponent]
    public class NavObstacleAuthoring : AuthoringBehaviour
    {
        [Tooltip("Blocked area (X by Z) centred on this transform.")]
        public Vector2 size = new(2f, 2f);

        public class Baker : Baker<NavObstacleAuthoring>
        {
            public override void Bake(NavObstacleAuthoring authoring)
            {
                var writer = this.Writer(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                NavSetup.AddObstacle(writer, authoring.size);
            }
        }
    }
}
