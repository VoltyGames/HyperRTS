using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Impacts
{
    /// <summary>Size, fade and placement of impact marks, apart from the projectors that draw them.</summary>
    public static class ImpactMarkMath
    {
        /// <summary>A random diameter in the mark's range, grown by the hit's splash radius.</summary>
        public static float Diameter(ImpactMark mark, float splashRadius, float random01) =>
            math.lerp(mark.size.x, mark.size.y, random01) + splashRadius * mark.sizePerSplashRadius;

        /// <summary>1 while fresh, then down to 0 over the fade time; a mark without a lifetime never fades.</summary>
        public static float Strength(float age, float lifetime, float fadeTime)
        {
            if (lifetime <= 0f || age <= lifetime)
            {
                return 1f;
            }

            return fadeTime > 0f ? math.saturate(1f - (age - lifetime) / fadeTime) : 0f;
        }

        /// <summary>The ground's normal from its height <paramref name="reach"/> metres either side of the point.</summary>
        public static float3 GroundNormal(in TerrainHeight terrain, float2 point, float reach)
        {
            var dx = terrain.Height(point + new float2(reach, 0f)) - terrain.Height(point - new float2(reach, 0f));
            var dz = terrain.Height(point + new float2(0f, reach)) - terrain.Height(point - new float2(0f, reach));
            return math.normalize(new float3(-dx, 2f * reach, -dz));
        }

        /// <summary>A projector's rotation: projecting down into the ground along its normal, turned by <paramref name="yaw"/>.</summary>
        public static quaternion Facing(float3 normal, float yaw)
        {
            var across = math.normalize(math.cross(new float3(1f, 0f, 0f), normal));
            var up = math.mul(quaternion.AxisAngle(normal, yaw), across);
            return quaternion.LookRotationSafe(-normal, up);
        }
    }
}
