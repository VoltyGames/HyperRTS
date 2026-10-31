using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// The heap a broken outer wall standing on the ground leaves at its foot. Every world places it at the same spot, so it is
    /// map scenery with an id of its own that multiplayer can crush or clear.
    /// </summary>
    public static class SectionRubble
    {
        /// <summary>Whether <paramref name="section"/> leaves a heap when it breaks.</summary>
        public static bool Leaves(in Section section) => section.Exterior && section.Anchored;

        /// <summary>The heap's pose: on the ground under the wall, turned with the building.</summary>
        public static LocalTransform Pose(in LocalTransform building, in Section section)
        {
            var foot = building.TransformPoint(new float3(section.Center.x, 0f, section.Center.z));
            return LocalTransform.FromPositionRotation(foot, building.Rotation);
        }

        public static void Spawn(EntityCommandBuffer ecb, Entity prefab, in LocalTransform building, in Section section)
        {
            var pose = Pose(building, section);
            var heap = ecb.Instantiate(prefab);
            ecb.SetComponent(heap, pose);
            ecb.AddComponent(heap, SceneryId.At(pose.Position, pose.Rotation));
        }
    }
}
