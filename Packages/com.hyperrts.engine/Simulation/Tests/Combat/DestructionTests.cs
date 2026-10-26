using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Destructible scenery, crushing, felled trees, remains that fall, and captured income.</summary>
    public class DestructionTests
    {
        private const float PoseTolerance = 0.01f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void HeavyCrusher_FlattensScenery_AndLeavesRemains()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var fence = SpawnScenery(new float3(5f, 0f, 0f), 1, remains);
            var tank = _world.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1.5f);
            _world.EntityManager.AddComponentData(tank, new Crusher { Size = 3 });

            _world.Run(0.5f);

            Assert.IsFalse(_world.EntityManager.Exists(fence), "the fence is flattened");
            using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Health>());
            Assert.AreEqual(5f, FindNear(query, new float3(5f, 0f, 0f)).x, 0.01f, "its remains lie where it stood");
        }

        [Test]
        public void Crusher_FlattensSceneryUnderItsWholeHull_ButNotBesideIt()
        {
            var underNose = SpawnScenery(new float3(1.5f, 0f, 3.5f), 1);
            var underTail = SpawnScenery(new float3(-1.5f, 0f, -3.5f), 1);
            var beside = SpawnScenery(new float3(3f, 0f, 0f), 1);
            var tank = _world.SpawnUnit(1, float3.zero, radius: 1.85f);
            var agent = _world.EntityManager.GetComponentData<NavAgent>(tank);
            agent.HullLength = 7.9f;
            _world.EntityManager.SetComponentData(tank, agent);
            _world.EntityManager.AddComponentData(tank, new Crusher { Size = 3 });

            _world.Run(0.5f);

            Assert.IsFalse(_world.EntityManager.Exists(underNose), "the hull's front corner reaches past a centre circle");
            Assert.IsFalse(_world.EntityManager.Exists(underTail), "so does its rear corner");
            Assert.IsTrue(_world.EntityManager.Exists(beside), "scenery clear of the tracks stands");
        }

        [Test]
        public void LightCrusher_LeavesSturdierSceneryStanding()
        {
            var tree = SpawnScenery(new float3(5f, 0f, 0f), 3);
            var jeep = _world.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1f);
            _world.EntityManager.AddComponentData(jeep, new Crusher { Size = 1 });

            _world.Run(0.5f);

            Assert.IsTrue(_world.EntityManager.Exists(tree));
            Assert.AreEqual(50f, _world.GetHealth(tree));
        }

        [Test]
        public void ConstructionSite_ClearsOpenSceneryInItsFootprint_ButNotSolidSceneryOrOutside()
        {
            var tree = SpawnScenery(new float3(2f, 0f, 1f), 0);
            var fence = SpawnScenery(new float3(-1f, 0f, -2f), 1);
            var outside = SpawnScenery(new float3(5f, 0f, 0f), 0);
            var wall = SpawnScenery(new float3(0f, 0f, 2.5f), 0, block: new float2(1f, 1f));
            _world.SpawnBuilding(1, float3.zero, new float2(6f, 6f), complete: false);

            _world.Run(0.5f);

            Assert.IsFalse(_world.EntityManager.Exists(tree), "a tree inside the site is cleared");
            Assert.IsFalse(_world.EntityManager.Exists(fence), "so is a fence");
            Assert.IsTrue(_world.EntityManager.Exists(outside), "scenery outside the footprint stands");
            Assert.IsTrue(_world.EntityManager.Exists(wall), "solid scenery is an obstacle, not cleared");
        }

        [Test]
        public void Splash_HitsNeutralScenery_ButSparesNeutralBuildings()
        {
            var tree = SpawnScenery(new float3(2f, 0f, 0f), 0);
            var house = _world.SpawnBuilding(0, new float3(-2f, 0f, 0f), new float2(2f, 2f));

            _world.QueueDamage(new DamageEvent { Position = float3.zero, SourceFaction = 1, Amount = 20f, Radius = 6f, EdgeFactor = 1f });
            _world.Tick();

            Assert.Less(_world.GetHealth(tree), 50f, "destructible scenery takes the blast");
            Assert.AreEqual(500f, _world.GetHealth(house), "plain neutral buildings don't");
        }

        [Test]
        public void Quantize_RoundTripsEveryHeading_FallingAwayFromTheSource()
        {
            var tree = new float3(3f, 0f, -2f);
            for (var fall = 0; fall < HeadingByte.Steps; fall++)
            {
                var source = tree - HeadingByte.Direction((byte)fall) * 7f;
                Assert.AreEqual(fall, ToppleMath.Quantize(source, tree), $"heading {fall}");
            }
        }

        [Test]
        public void RemainsPose_PointsTheCrownAlongTheFall()
        {
            var standing = LocalTransform.FromPositionRotationScale(new float3(1f, 2f, 3f), quaternion.RotateY(1f), 1.5f);
            const float lie = 0.7f;

            var pose = ToppleMath.RemainsPose(standing, 64, lie);

            var canopy = math.rotate(pose.Rotation, new float3(math.sin(lie), 0f, math.cos(lie)));
            MathAssert.Near(new float3(1f, 0f, 0f), canopy, PoseTolerance);
            MathAssert.Near(standing.Position, pose.Position, PoseTolerance);
            Assert.AreEqual(1.5f, pose.Scale);
        }

        [Test]
        public void Fell_StartsWithTheTrunkWhereItStood_AndEndsAtTheRemainsPose()
        {
            var standing = LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(0.4f)).ApplyScale(2f);
            var topple = new Topple { Trunk = math.normalize(new float3(0.1f, 1f, 0f)), Height = 7f, Lie = 0.3f };

            var toppleFall = ToppleMath.Fell(standing, topple, 200, true, out var pose);

            var canopy = new float3(math.sin(topple.Lie), 0f, math.cos(topple.Lie));
            var upright = ToppleMath.Pose(toppleFall, pose.Rotation, 0f);
            MathAssert.Near(math.rotate(standing.Rotation, topple.Trunk), math.rotate(upright, canopy), PoseTolerance);
            var down = ToppleMath.Pose(toppleFall, pose.Rotation, 1f);
            MathAssert.Near(HeadingByte.Direction(200), math.rotate(down, canopy), PoseTolerance);
            MathAssert.Near(math.rotate(pose.Rotation, canopy), HeadingByte.Direction(200), PoseTolerance);
            MathAssert.Near(HeadingByte.Direction(200), toppleFall.CanopyDirection, PoseTolerance);
            Assert.AreEqual(14f, toppleFall.Height, 1e-4f, "scaled with the tree");
        }

        [Test]
        public void LethalSplash_FellsTheTreeAwayFromTheBlast_InThePoseClientsDerive()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var tree = SpawnTree(new float3(5f, 0f, 0f), remains, out var standing, out var topple);

            _world.QueueDamage(new DamageEvent { Position = float3.zero, Amount = 999f, Radius = 8f, EdgeFactor = 1f });
            _world.Tick();

            Assert.IsFalse(_world.EntityManager.Exists(tree));
            var fall = ToppleMath.Quantize(float3.zero, standing.Position);
            MathAssert.Near(new float3(1f, 0f, 0f), HeadingByte.Direction(fall), PoseTolerance);
            var expected = ToppleMath.Fell(standing, topple, fall, true, out var pose);
            var (shown, toppleFall) = FellRemains();
            MathAssert.Near(pose.Position, shown.Position, PoseTolerance);
            Assert.Greater(math.abs(math.dot(pose.Rotation.value, shown.Rotation.value)), 1f - 1e-3f, "same pose");
            Assert.Greater(math.abs(math.dot(expected.Tilt.value, toppleFall.Tilt.value)), 1f - 1e-3f, "same fall");
        }

        [Test]
        public void DirectHit_FellsTheTreeAwayFromTheShooter()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var tree = SpawnTree(new float3(5f, 0f, 0f), remains, out _, out _);

            _world.QueueDamage(new DamageEvent { Target = tree, Origin = new float3(5f, 0f, -10f), Amount = 999f });
            _world.Tick();

            var (shown, _) = FellRemains();
            MathAssert.Near(new float3(0f, 0f, 1f), math.rotate(shown.Rotation, new float3(0f, 0f, 1f)), PoseTolerance);
        }

        [Test]
        public void AircraftKilledAtAltitude_ItsRemainsFallToTheGround()
        {
            _world.CreateTerrain(_ => 2f);
            var wreck = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var aircraft = _world.SpawnAircraft(1, new float3(5f, 2f, 0f));
            _world.EntityManager.AddComponentData(aircraft, new SpawnOnDeath { Prefab = wreck });
            _world.Run(1f);
            Assert.AreEqual(12f, _world.GetPosition(aircraft).y, 1e-3f, "flying");

            _world.EntityManager.SetComponentData(aircraft, new Health { Current = 0f, Max = 100f });
            _world.Tick(frames: 3);
            using var falling = _world.All<FallingRemains>();
            Assert.AreEqual(1, falling.Length);
            var remains = falling[0];
            Assert.Greater(_world.GetPosition(remains).y, 10f, "the wreck starts where the aircraft died");

            Assert.IsTrue(_world.RunUntil(() => !_world.EntityManager.HasComponent<FallingRemains>(remains), 3f));
            Assert.AreEqual(2f, _world.GetPosition(remains).y, 1e-3f, "and lands on the ground");
        }

        [Test]
        public void ResourceIncome_PaysOwnerWithFirstOwnerBonus_AndNothingWhileNeutral()
        {
            var supplies = _world.CreateSupplies();
            var derrick = _world.SpawnBuilding(0, float3.zero, new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, derrick);
            writer.Add(new ResourceIncome { Type = supplies, Amount = 10, Interval = 5f, FirstOwnerBonus = 100 });

            _world.Run(6f);
            Assert.AreEqual(0, _world.GetResources(1, supplies), "a neutral derrick pays no one");

            _world.EntityManager.SetComponentData(derrick, new Faction { Value = 1 });
            _world.Run(11f);
            Assert.AreEqual(120, _world.GetResources(1, supplies), "the bonus plus two payments");
        }

        private Entity SpawnScenery(float3 position, int crush, Entity remains = default, float2 block = default)
        {
            var entity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            DestructibleSetup.AddScenery(ref writer, 50f, crush, remains, block,
                SceneryId.At(position, quaternion.identity));
            return entity;
        }

        private Entity SpawnTree(float3 position, Entity remains, out LocalTransform standing, out Topple topple)
        {
            var entity = _world.EntityManager.CreateEntity();
            standing = LocalTransform.FromPositionRotation(position, quaternion.RotateY(0.5f));
            topple = new Topple { Trunk = new float3(0f, 1f, 0f), Height = 12f, Lie = 0f, CanopyRadius = 3f };
            _world.EntityManager.AddComponentData(entity, standing);
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            DestructibleSetup.AddScenery(ref writer, 50f, 0, remains, float2.zero,
                SceneryId.At(position, standing.Rotation));
            DestructibleSetup.AddTopple(ref writer, topple.Trunk, topple.Height, topple.Lie, topple.CanopyRadius);
            return entity;
        }

        private (LocalTransform, ToppleFall) FellRemains()
        {
            using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ToppleFall>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "one felled remains");
            var entity = entities[0];
            return (_world.EntityManager.GetComponentData<LocalTransform>(entity),
                _world.EntityManager.GetComponentData<ToppleFall>(entity));
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
