using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// How a building looks while it is built: site dressing shown until complete, a work rig at its work points while
    /// construction is moving, and a rig played once when it completes. Any of them may be empty.
    /// </summary>
    public struct ConstructionSite : IComponentData
    {
        /// <summary>Child holding the dressing (fencing, crane, pallets, lights); hidden once complete.</summary>
        public Entity Visual;

        /// <summary>Looping rig streamed at each <see cref="ConstructionWorkPoint"/> while construction is moving.</summary>
        public UnityObjectRef<GameObject> WorkRig;

        /// <summary>Rig played once at the building when a client sees its construction complete.</summary>
        public UnityObjectRef<GameObject> CompleteEffect;
    }

    /// <summary>A point the <see cref="ConstructionSite.WorkRig"/> streams from, in the building's local space.</summary>
    [InternalBufferCapacity(0)]
    public struct ConstructionWorkPoint : IBufferElementData
    {
        public float3 Socket;
    }

    /// <summary>
    /// What presentation last saw of a building's construction; the server never reads it. Disabled once the building
    /// shows built, so finished buildings cost nothing until construction starts again.
    /// </summary>
    public struct ConstructionWatch : IComponentData, IEnableableComponent
    {
        /// <summary>Work counts as stalled once progress hasn't risen for this long.</summary>
        public const double StallSeconds = 0.5;

        public ConstructionLook Look;
        public float Progress;
        public double RoseAt;

        /// <summary>Whether the site dressing shows: while construction is under way.</summary>
        public readonly bool ShowsSite => Look == ConstructionLook.Building;

        /// <summary>Takes this frame's state; true on the one frame a construction seen in progress completes.</summary>
        public bool Observe(bool building, float progress, double now)
        {
            var completed = Look == ConstructionLook.Building && !building;
            var rose = Look == ConstructionLook.Building && building && progress > Progress;
            if (Look != ConstructionLook.Building && building)
            {
                // First sight isn't a rise: a ghost seen mid-build may be stalled.
                RoseAt = double.NegativeInfinity;
            }

            if (rose)
            {
                RoseAt = now;
            }

            Look = building ? ConstructionLook.Building : ConstructionLook.Built;
            Progress = progress;
            return completed;
        }

        /// <summary>Whether progress rose within the last <see cref="StallSeconds"/>.</summary>
        public readonly bool IsWorking(double now) => Look == ConstructionLook.Building && now - RoseAt < StallSeconds;
    }

    /// <summary>A building's construction as presentation last saw it.</summary>
    public enum ConstructionLook : byte
    {
        Unseen,
        Building,
        Built,
    }
}
