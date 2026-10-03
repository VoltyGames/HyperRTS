using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Blocks a box of the pathfinding grid (rocks, cliffs, water). Buildings block their footprint already.</summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Nav Obstacle")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class NavObstacleAuthoring : MonoBehaviour
    {
        [Tooltip("Blocked area (X by Z) centred on this transform.")]
        public Vector2 size = new(2f, 2f);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.1f, size.y));
        }

        public class Baker : Baker<NavObstacleAuthoring>
        {
            public override void Bake(NavObstacleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new NavObstacle { Size = authoring.size });
            }
        }
    }

    /// <summary>Axis-aligned XZ box that pathing and building placement treat as solid.</summary>
    public struct NavObstacle : IComponentData
    {
        public float2 Size;
    }
}
