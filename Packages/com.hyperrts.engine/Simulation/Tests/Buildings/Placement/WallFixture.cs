using System.Collections.Generic;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Buildings.Placement
{
    /// <summary>
    /// A 2 by 1.2 m wall bought at 100 supplies a segment, its builders and its sites, and the leg most tests draw:
    /// four segments from x 4 to 10 along z 0.5.
    /// </summary>
    public abstract class WallFixture : SimulationFixture
    {
        protected static readonly float2 Footprint = new(2f, 1.2f);
        protected static readonly float3 LegStart = new(4f, 0f, 0.3f);
        protected static readonly float3 LegEnd = new(10.2f, 0f, 0.3f);

        private static readonly float3 Far = new(90f, 0f, 90f);

        protected ResourceType Supplies;

        [SetUp]
        public void PrepareWall()
        {
            Supplies = World.CreateSupplies();
        }

        protected Entity WallPrefab(int funds = 1000)
        {
            World.AddResources(1, Supplies, funds);
            var prefab = World.MakePrefab(World.SpawnBuilding(0, Far, Footprint, new TestBuilding { Name = "Wall" }));
            World.EntityManager.AddComponent<LinePlacement>(prefab);
            World.SetCost(prefab, Supplies, 100);
            return prefab;
        }

        protected Entity Builder(Entity prefab, float3 position) =>
            World.MakeBuilder(World.SpawnUnit(1, position), prefab);

        /// <summary>The leg from <see cref="LegStart"/> to <see cref="LegEnd"/>, as the server lays it.</summary>
        protected PlacementLine Leg() =>
            new(World.Singleton<MapSettings>(), LegStart, LegEnd, Footprint, PlacementSurface.Land, LineStart.Snapped);

        protected void SetBuildingGap(float gap)
        {
            var map = World.Singleton<MapSettings>();
            map.BuildingGap = gap;
            World.Set(World.MatchEntity(), map);
        }

        /// <summary>Lays the leg, then a second one joined to its end, <paramref name="length"/> metres long.</summary>
        protected void DrawCorner(Entity builder, Entity prefab, float2 direction, float length)
        {
            World.PlaceLine(builder, prefab, LegStart, LegEnd);
            World.Tick();
            var start = Leg().JoinedStart(direction);
            World.PlaceLine(builder, prefab, start, start + new float3(direction.x, 0f, direction.y) * length,
                new TestLine
            {
                Join = LineStart.Joined,
                Queue = true,
            });
            World.Tick();
        }

        /// <summary>The player's unbuilt sites, ordered along X then Z.</summary>
        protected NativeList<float3> SitePositions()
        {
            var result = new NativeList<float3>(Allocator.Temp);
            foreach (var site in World.All<ConstructionProgress>())
            {
                if (World.Get<Owner>(site).Value == 1)
                {
                    result.Add(World.Get<LocalTransform>(site).Position);
                }
            }

            result.Sort(new AlongLine());
            return result;
        }

        protected NavBox[] SiteBoxes()
        {
            var sites = World.All<ConstructionProgress>();
            var boxes = new NavBox[sites.Length];
            for (var i = 0; i < sites.Length; i++)
            {
                var transform = World.Get<LocalTransform>(sites[i]);
                boxes[i] = new NavBox(transform.Position, transform.Rotation, World.Get<NavObstacle>(sites[i]).Size);
            }

            return boxes;
        }

        protected static void AssertOnlyTouch(NavBox[] boxes)
        {
            for (var i = 0; i < boxes.Length; i++)
            {
                for (var j = i + 1; j < boxes.Length; j++)
                {
                    Assert.IsFalse(boxes[i].Overlaps(boxes[j]), $"segments {i} and {j} only touch");
                }
            }
        }

        // X first, Z on a tie; floats a rounding apart tie, as segments of one line along Z do.
        private struct AlongLine : IComparer<float3>
        {
            public int Compare(float3 a, float3 b) =>
                math.abs(a.x - b.x) > 1e-3f ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);
        }
    }
}
