using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Placement
{
    /// <summary>A standing footprint a placement spot must clear; a line piece keeps no building gap from others.</summary>
    public readonly struct PlacedFootprint
    {
        public readonly NavBox Box;
        public readonly bool InLine;

        public PlacedFootprint(in NavBox box, bool inLine)
        {
            Box = box;
            InLine = inLine;
        }

        /// <summary>
        /// The footprints of <paramref name="query"/>'s entities, which carry a <see cref="NavObstacle"/> and a
        /// <see cref="LocalTransform"/>, copied once so many spots are checked against one read.
        /// </summary>
        public static NativeList<PlacedFootprint> Read(EntityManager entityManager, EntityQuery query)
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            var obstacles = query.ToComponentDataArray<NavObstacle>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var footprints = new NativeList<PlacedFootprint>(obstacles.Length, Allocator.Temp);
            for (var i = 0; i < obstacles.Length; i++)
            {
                var box = new NavBox(transforms[i].Position, transforms[i].Rotation, obstacles[i].Size);
                footprints.Add(new PlacedFootprint(box, entityManager.HasComponent<LinePlacement>(entities[i])));
            }

            return footprints;
        }
    }
}
