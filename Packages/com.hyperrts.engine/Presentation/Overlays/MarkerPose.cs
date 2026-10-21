using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Where a unit's ground marker sits, read from the pose the model is drawn with (its <see cref="LocalToWorld"/>,
    /// which presentation may turn away from the simulation transform), so the marker never trails the model.
    /// </summary>
    public readonly struct MarkerPose
    {
        // Below this the marker stops following the terrain, so ships and bridge traffic keep theirs at the surface.
        private const float FloorDrop = 2f;

        // Far below any terrain, so the marker always lies on the ground.
        private const float NoFloor = -10000f;

        public readonly float3 Position;
        public readonly quaternion Facing;

        /// <summary>Lowest height the marker may sit at; aircraft have none and mark the ground under them.</summary>
        public readonly float Floor;

        public MarkerPose(in LocalToWorld world, NavLayer layer)
        {
            Position = world.Position;
            var forward = new float3(world.Forward.x, 0f, world.Forward.z);
            Facing = quaternion.LookRotationSafe(forward, math.up());
            Floor = layer == NavLayer.Air ? NoFloor : Position.y - FloorDrop;
        }

        /// <summary>Unit grid scaled to the footprint at the floor height; the shader drapes it on the ground.</summary>
        public Matrix4x4 Matrix(in MarkerFootprint footprint) => Matrix4x4.TRS(new Vector3(Position.x, Floor, Position.z), Facing,
            new Vector3(footprint.HalfSize.x * 2f, 1f, footprint.HalfSize.y * 2f));
    }
}
