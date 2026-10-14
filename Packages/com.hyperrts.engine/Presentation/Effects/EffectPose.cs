using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Where and which way a rig plays; rigs face their +Z.</summary>
    public static class EffectPose
    {
        /// <summary>Facing along a direction; straight up when it has none.</summary>
        public static Quaternion Facing(Vector3 direction) =>
            direction.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(direction) : Up;

        /// <summary>
        /// An impact's pose: on the terrain for ground hits, facing up or along the ground's normal, turned about that
        /// axis by <paramref name="spin"/> radians for variety.
        /// </summary>
        public static (Vector3 Position, Quaternion Rotation) Impact(in ImpactEvent impact, in TerrainHeight terrain,
            bool hasTerrain, bool alignToGround, float spin)
        {
            var onGround = impact.Surface == ImpactSurface.Ground && hasTerrain;
            if (!onGround)
            {
                return (impact.Position, Up * Quaternion.Euler(0f, 0f, spin * Mathf.Rad2Deg));
            }

            var point = impact.Position.xz;
            var position = new float3(point.x, terrain.Height(point), point.y);
            var normal = alignToGround ? (Vector3)ImpactMarkMath.GroundNormal(terrain, point, 1f) : Vector3.up;
            var rotation = Quaternion.LookRotation(normal, Vector3.forward);
            return (position, rotation * Quaternion.Euler(0f, 0f, spin * Mathf.Rad2Deg));
        }

        private static Quaternion Up => Quaternion.LookRotation(Vector3.up, Vector3.forward);
    }
}
