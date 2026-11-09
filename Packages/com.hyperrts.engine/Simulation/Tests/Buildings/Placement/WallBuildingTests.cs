using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Buildings.Placement
{
    /// <summary>
    /// Builders on a wall: they queue its segments from the nearer end, run on through its corners, and a Build order
    /// on any one segment builds the whole wall.
    /// </summary>
    public class WallBuildingTests : WallFixture
    {
        [Test]
        public void PlacedLine_QueuesBuildOrdersFromTheNearerEnd()
        {
            var prefab = WallPrefab();
            var builder = Builder(prefab, new float3(14f, 0f, 0f));
            World.PlaceLine(builder, prefab, LegStart, LegEnd);
            World.Tick();

            var active = World.Get<ActiveOrder>(builder).Value;
            Assert.AreEqual(OrderType.Build, active.Type);
            Assert.AreEqual(10f, active.Position.x, 1e-4f);
            var queued = World.Buffer<QueuedOrder>(builder);
            Assert.AreEqual(3, queued.Length);
            for (var i = 0; i < queued.Length; i++)
            {
                Assert.AreEqual(OrderType.Build, queued[i].Value.Type);
                Assert.AreEqual(8f - 2f * i, queued[i].Value.Position.x, 1e-4f);
            }
        }

        [Test]
        public void QueuedCorner_IsBuiltFromWhereTheFirstLegLeavesTheBuilders()
        {
            var prefab = WallPrefab();
            // Nearer the far end of the second leg than its corner, but the first leg ends at the corner.
            var builder = Builder(prefab, new float3(4f, 0f, 8f));
            DrawCorner(builder, prefab, new float2(0f, 1f), 6f);

            var queued = World.Buffer<QueuedOrder>(builder);
            Assert.AreEqual(7, queued.Length);
            Assert.AreEqual(10f, queued[2].Value.Position.x, 1e-4f, "the first leg runs out to the corner");
            Assert.AreEqual(2.1f, queued[3].Value.Position.z, 1e-4f, "the second leg starts at the corner");
            Assert.Greater(queued[6].Value.Position.z, queued[3].Value.Position.z);
        }

        [Test]
        public void BuildOrderOnOneSegment_BuildsTheWholeWall_RoundItsCorner()
        {
            var prefab = WallPrefab();
            World.SetBuildTime(prefab, 0.5f);
            var placer = Builder(prefab, new float3(-20f, 0f, -20f));
            DrawCorner(placer, prefab, new float2(0f, 1f), 6f);
            World.EntityManager.DestroyEntity(placer);
            var builder = Builder(prefab, new float3(8f, 0f, -3f));
            var middle = Array.Find(World.All<ConstructionProgress>().ToArray(),
                site => math.distance(World.Get<LocalTransform>(site).Position.xz, new float2(8f, 0.5f)) < 1e-3f);

            World.Order(builder, OrderType.Build, middle);

            Assert.IsTrue(World.RunUntil(() => World.All<ConstructionProgress>().Length == 0, 60f),
                "every segment of both legs is built from the one order");
        }
    }
}
