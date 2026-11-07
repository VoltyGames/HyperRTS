using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Movement.Crushing;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Resources.Income;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>Destructible scenery, crushing, remains that fall, and captured income.</summary>
    public class DestructionTests : SimulationFixture
    {
        [Test]
        public void HeavyCrusher_FlattensScenery_AndLeavesRemains()
        {
            var remains = World.MakePrefab(World.EntityManager.CreateEntity(typeof(LocalTransform)));
            var fence = SpawnScenery(new float3(5f, 0f, 0f), 1, remains);
            var tank = World.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1.5f);
            World.Add(tank, new Crusher { Size = 3 });

            World.Run(0.5f);

            Assert.IsFalse(World.EntityManager.Exists(fence), "the fence is flattened");
            using var query = World.Query(ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Health>());
            Assert.AreEqual(5f, FindNear(query, new float3(5f, 0f, 0f)).x, 0.01f, "its remains lie where it stood");
        }

        [Test]
        public void Crusher_FlattensSceneryUnderItsWholeHull_ButNotBesideIt()
        {
            var underNose = SpawnScenery(new float3(1.5f, 0f, 3.5f), 1);
            var underTail = SpawnScenery(new float3(-1.5f, 0f, -3.5f), 1);
            var beside = SpawnScenery(new float3(3f, 0f, 0f), 1);
            var tank = World.SpawnUnit(1, float3.zero, radius: 1.85f);
            var agent = World.Get<NavAgent>(tank);
            agent.HullLength = 7.9f;
            World.Set(tank, agent);
            World.Add(tank, new Crusher { Size = 3 });

            World.Run(0.5f);

            Assert.IsFalse(World.EntityManager.Exists(underNose), "the hull's front corner reaches past a centre circle");
            Assert.IsFalse(World.EntityManager.Exists(underTail), "so does its rear corner");
            Assert.IsTrue(World.EntityManager.Exists(beside), "scenery clear of the tracks stands");
        }

        [Test]
        public void TankCrossingAFenceLine_LeavesAGapOneSegmentWide_AndTheRestStillBlocks()
        {
            const float segment = 2.5f;
            var fence = new List<Entity>();
            for (var i = -3; i <= 3; i++)
            {
                fence.Add(SpawnScenery(new float3(i * segment, 0f, 0f), 1, block: new float2(segment, 0.3f)));
            }

            var tank = World.SpawnUnit(1, float3.zero, radius: 0.9f);
            World.Add(tank, new Crusher { Size = 3 });
            World.Run(0.5f);

            for (var i = 0; i < fence.Count; i++)
            {
                Assert.AreEqual(i == 3, !World.EntityManager.Exists(fence[i]), $"segment {i}");
            }

            var grid = World.Grid();
            Assert.IsTrue(grid.IsWalkable(new float3(0f, 0f, 0f)), "the gap is open");
            Assert.IsFalse(grid.IsWalkable(new float3(segment * 2f, 0f, 0f)), "the next segment still blocks");
        }

        [Test]
        public void LightCrusher_LeavesSturdierSceneryStanding()
        {
            var tree = SpawnScenery(new float3(5f, 0f, 0f), 3);
            var jeep = World.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1f);
            World.Add(jeep, new Crusher { Size = 1 });

            World.Run(0.5f);

            Assert.IsTrue(World.EntityManager.Exists(tree));
            Assert.AreEqual(50f, World.GetHealth(tree));
        }

        [Test]
        public void ConstructionSite_ClearsOpenSceneryInItsFootprint_ButNotSolidSceneryOrOutside()
        {
            var tree = SpawnScenery(new float3(2f, 0f, 1f), 0);
            var fence = SpawnScenery(new float3(-1f, 0f, -2f), 1);
            var outside = SpawnScenery(new float3(5f, 0f, 0f), 0);
            var wall = SpawnScenery(new float3(0f, 0f, 2.5f), 0, block: new float2(1f, 1f));
            World.SpawnBuilding(1, float3.zero, new float2(6f, 6f), complete: false);

            World.Run(0.5f);

            Assert.IsFalse(World.EntityManager.Exists(tree), "a tree inside the site is cleared");
            Assert.IsFalse(World.EntityManager.Exists(fence), "so is a fence");
            Assert.IsTrue(World.EntityManager.Exists(outside), "scenery outside the footprint stands");
            Assert.IsTrue(World.EntityManager.Exists(wall), "solid scenery is an obstacle, not cleared");
        }

        [Test]
        public void Splash_HitsNeutralScenery_ButSparesNeutralBuildings()
        {
            var tree = SpawnScenery(new float3(2f, 0f, 0f), 0);
            var house = World.SpawnBuilding(0, new float3(-2f, 0f, 0f), new float2(2f, 2f));

            World.QueueDamage(new DamageEvent { Position = float3.zero, SourceOwner = 1, Layers = WeaponTargets.Surface, Profile = new DamageProfile { Amount = 20f, Radius = 6f, EdgeFactor = 1f } });
            World.Tick();

            Assert.Less(World.GetHealth(tree), 50f, "destructible scenery takes the blast");
            Assert.AreEqual(500f, World.GetHealth(house), "plain neutral buildings don't");
        }

        [Test]
        public void AircraftKilledAtAltitude_ItsRemainsFallToTheGround()
        {
            World.CreateTerrain(_ => 2f);
            var wreck = World.MakePrefab(World.EntityManager.CreateEntity(typeof(LocalTransform)));
            var aircraft = World.SpawnAircraft(1, new float3(5f, 2f, 0f));
            World.Add(aircraft, new SpawnOnDeath { Prefab = wreck });
            World.Run(1f);
            Assert.AreEqual(12f, World.GetPosition(aircraft).y, 1e-3f, "flying");

            World.Set(aircraft, new Health { Current = 0f, Max = 100f });
            World.Tick(frames: 3);
            using var falling = World.All<FallingRemains>();
            Assert.AreEqual(1, falling.Length);
            var remains = falling[0];
            Assert.Greater(World.GetPosition(remains).y, 10f, "the wreck starts where the aircraft died");

            Assert.IsTrue(World.RunUntil(() => !World.EntityManager.HasComponent<FallingRemains>(remains), 3f));
            Assert.AreEqual(2f, World.GetPosition(remains).y, 1e-3f, "and lands on the ground");
        }

        [Test]
        public void ResourceIncome_PaysOwnerWithFirstOwnerBonus_AndNothingWhileNeutral()
        {
            var supplies = World.CreateSupplies();
            var derrick = World.SpawnBuilding(0, float3.zero, new float2(4f, 4f));
            var writer = World.Writer(derrick);
            writer.Add(new ResourceIncome { Type = supplies, Amount = 10, Interval = 5f, FirstOwnerBonus = 100 });

            World.Run(6f);
            Assert.AreEqual(0, World.GetResources(1, supplies), "a neutral derrick pays no one");

            World.Set(derrick, new Owner { Value = 1 });
            World.Run(11f);
            Assert.AreEqual(120, World.GetResources(1, supplies), "the bonus plus two payments");
        }

        private Entity SpawnScenery(float3 position, int crush, Entity remains = default, float2 block = default)
        {
            var entity = World.EntityManager.CreateEntity();
            World.Add(entity, LocalTransform.FromPosition(position));
            var writer = World.Writer(entity);
            DestructibleAuthoring.AddScenery(writer, 50f, crush, remains, block,
                SceneryId.At(position, quaternion.identity));
            return entity;
        }

        private static float3 FindNear(EntityQuery query, float3 point)
        {
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            foreach (var transform in transforms)
            {
                if (math.distance(transform.Position, point) < 0.1f)
                {
                    return transform.Position;
                }
            }

            return new float3(float.NaN);
        }
    }
}
