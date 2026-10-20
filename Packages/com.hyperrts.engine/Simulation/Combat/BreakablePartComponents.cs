using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A wall or roof section shown intact or broken; positions are in the building's local space.</summary>
    public struct BreakablePart : IBufferElementData
    {
        public Entity Intact;
        public Entity Broken;
        public float3 Centre;

        /// <summary>Horizontal, or up for a part over the centre.</summary>
        public float3 Outward;

        /// <summary>0 while intact, else its place in the break order, so repair restores the latest.</summary>
        public int Order;

        public readonly quaternion EffectFacing =>
            quaternion.LookRotation(Outward, math.abs(Outward.y) > 0.5f ? new float3(0f, 0f, 1f) : math.up());
    }

    /// <summary>How a building's <see cref="BreakablePart"/>s break, and its bounds for matching impacts.</summary>
    public struct BreakableParts : IComponentData
    {
        /// <summary>Metres; a blast beside a wall still picks that side.</summary>
        public const float HitMargin = 2f;

        public float BreakShare;

        public UnityObjectRef<GameObject> Effect;

        public float3 BoundsCentre;
        public float3 BoundsExtents;

        public readonly int TargetBroken(int partCount, float healthFraction) =>
            (int)math.round(partCount * math.saturate(BreakShare) * (1f - math.saturate(healthFraction)));

        public readonly bool IsNear(float3 local) =>
            math.all(math.abs(local - BoundsCentre) <= BoundsExtents + HitMargin);
    }

    /// <summary>The breakage on screen and the latest hit on the building; presentation keeps it.</summary>
    public struct ShownBreakage : IComponentData
    {
        public int Broken;

        public float3 LastHit;

        public bool HasHit;

        /// <summary>False until shown, so a loaded or unseen building syncs without effects.</summary>
        public bool Seen;

        /// <summary>Without a hit (late join, fog) bake order keeps every client's pick the same.</summary>
        public readonly int NextToBreak(NativeArray<BreakablePart> parts)
        {
            var next = -1;
            var nearest = float.MaxValue;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Order != 0)
                {
                    continue;
                }

                if (!HasHit)
                {
                    return i;
                }

                var distance = math.distancesq(parts[i].Centre, LastHit);
                if (distance < nearest)
                {
                    nearest = distance;
                    next = i;
                }
            }

            return next;
        }

        public readonly int LatestBroken(NativeArray<BreakablePart> parts)
        {
            if (Broken <= 0)
            {
                return -1;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Order == Broken)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
