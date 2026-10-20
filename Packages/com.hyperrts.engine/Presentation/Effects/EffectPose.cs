using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Where and which way a rig plays; rigs are authored with +Y up and +Z forward.</summary>
    public static class EffectPose
    {
        /// <summary>Forward along a direction, kept upright; unturned when it has none.</summary>
        public static Quaternion Facing(Vector3 direction) =>
            direction.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(direction) : Quaternion.identity;

        /// <summary>
        /// An impact cue's pose: on the terrain for ground hits, up along the world or the ground's normal. The rig's
        /// random spin turns it about that axis.
        /// </summary>
        public static (Vector3 Position, Quaternion Rotation) Impact(in CueEvent impact, in TerrainHeight terrain,
            bool hasTerrain, bool alignToGround)
        {
            var onGround = impact.Surface == ImpactSurface.Ground && hasTerrain;
            if (!onGround)
            {
                return (impact.Position, Quaternion.identity);
            }

            var point = impact.Position.xz;
            var position = new float3(point.x, terrain.Height(point), point.y);
            var normal = alignToGround ? (Vector3)ImpactMarkMath.GroundNormal(terrain, point, 1f) : Vector3.up;
            return (position, Quaternion.FromToRotation(Vector3.up, normal));
        }
    }
}
