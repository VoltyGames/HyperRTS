using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Cursors;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Tests;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The cursor intent decision table, end to end through the right-click order rules.</summary>
    public class CursorIntentTests
    {
        private static readonly float2 Footprint = new(4f, 4f);

        private TestWorld _world;
        private SystemHandle _intent;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.World.CreateSystem<ClientSingletonSystem>();
            _intent = _world.World.CreateSystem<CursorIntentSystem>();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Tank_OnGround_Moves()
        {
            Select(Tank(float3.zero));

            Assert.AreEqual(CursorKind.Move, Decide(Entity.Null));
        }

        [Test]
        public void Tank_OnVisibleEnemy_Attacks()
        {
            Select(Tank(float3.zero));
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));
            _world.Tick();

            Assert.AreEqual(CursorKind.Attack, Decide(enemy));
        }

        [Test]
        public void Engineer_OnNeutralPost_Captures()
        {
            Select(Engineer(float3.zero));

            Assert.AreEqual(CursorKind.Capture, Decide(Post()));
        }

        [Test]
        public void MixedSelection_ShowsTheHighestOrder()
        {
            Select(Tank(float3.zero));
            Select(Engineer(new float3(2f, 0f, 0f)));

            Assert.AreEqual(CursorKind.Capture, Decide(Post()));
        }

        [Test]
        public void Truck_OnResourceNode_Supplies()
        {
            Select(_world.MakeHarvester(_world.SpawnUnit(1, float3.zero), 10, 1f));
            var node = _world.SpawnNode(new float3(6f, 0f, 0f), _world.CreateSupplies(), 500);

            Assert.AreEqual(CursorKind.Supply, Decide(node));
        }

        [Test]
        public void Infantry_OnOwnContainer_Garrisons()
        {
            var rifleman = _world.SpawnUnit(1, float3.zero);
            var writer = new EntityManagerWriter(_world.EntityManager, rifleman);
            TransportSetup.AddPassenger(ref writer, 1);
            Select(rifleman);
            var bunker = _world.SpawnBuilding(1, new float3(8f, 0f, 0f), Footprint);
            writer = new EntityManagerWriter(_world.EntityManager, bunker);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 4, MaxPassengerSize = 1 });

            Assert.AreEqual(CursorKind.Garrison, Decide(bunker));
        }

        [Test]
        public void Dozer_OnDamagedOwnBuilding_Repairs()
        {
            Select(_world.MakeBuilder(_world.SpawnUnit(1, float3.zero)));
            var barracks = _world.SpawnBuilding(1, new float3(8f, 0f, 0f), Footprint);
            _world.EntityManager.SetComponentData(barracks, new Health { Current = 200f, Max = 500f });

            Assert.AreEqual(CursorKind.Repair, Decide(barracks));
        }

        [Test]
        public void NoSelection_SelectsWhatItPointsAt()
        {
            var tank = Tank(float3.zero);

            Assert.AreEqual(CursorKind.Select, Decide(tank));
            Assert.AreEqual(CursorKind.Default, Decide(Entity.Null));
        }

        [Test]
        public void Tank_OnOwnUnit_Selects()
        {
            Select(Tank(float3.zero));

            Assert.AreEqual(CursorKind.Select, Decide(Tank(new float3(3f, 0f, 0f))));
        }

        [Test]
        public void Producer_OnGround_SetsRally()
        {
            Select(_world.MakeProducer(_world.SpawnBuilding(1, float3.zero, Footprint), new float3(0f, 0f, 4f)));

            Assert.AreEqual(CursorKind.Rally, Decide(Entity.Null));
        }

        [Test]
        public void Placement_ShowsBuildOrInvalid()
        {
            SetSingleton(new PlacementState { Active = true, Valid = true });
            Assert.AreEqual(CursorKind.Build, Decide(Entity.Null));

            SetSingleton(new PlacementState { Active = true, Valid = false });
            Assert.AreEqual(CursorKind.Invalid, Decide(Entity.Null));
        }

        [Test]
        public void PendingAttackMove_BeatsTheRightClick()
        {
            Select(Tank(float3.zero));
            SetSingleton(new PendingCommand { Type = CommandType.AttackMove });

            Assert.AreEqual(CursorKind.AttackMove, Decide(Entity.Null));
        }

        [Test]
        public void PointerOverUI_ShowsDefault_ButEdgeScrollWins()
        {
            Select(Tank(float3.zero));
            SetSingleton(new PointerState { OverUI = true });
            Assert.AreEqual(CursorKind.Default, Decide(Entity.Null));

            SetSingleton(new ScrollEdge { Direction = new int2(1, 1) });
            Assert.AreEqual(CursorKind.ScrollUpRight, Decide(Entity.Null));
        }

        [TestCase(-1, 1, CursorKind.ScrollUpLeft)]
        [TestCase(0, 1, CursorKind.ScrollUp)]
        [TestCase(1, 1, CursorKind.ScrollUpRight)]
        [TestCase(-1, 0, CursorKind.ScrollLeft)]
        [TestCase(1, 0, CursorKind.ScrollRight)]
        [TestCase(-1, -1, CursorKind.ScrollDownLeft)]
        [TestCase(0, -1, CursorKind.ScrollDown)]
        [TestCase(1, -1, CursorKind.ScrollDownRight)]
        public void Scroll_PointsAlongTheEdge(int x, int y, CursorKind expected) =>
            Assert.AreEqual(expected, CursorIntent.Scroll(new int2(x, y)));

        [TestCase(0f, 500f, -1, 0)]
        [TestCase(1919f, 1079f, 1, 1)]
        [TestCase(960f, 540f, 0, 0)]
        [TestCase(-5f, 540f, 0, 0)]
        public void ScrollEdge_ComesFromThePointer(float x, float y, int edgeX, int edgeY) =>
            Assert.AreEqual(new int2(edgeX, edgeY), ScrollEdge.At(new float2(x, y), new float2(1920f, 1080f), 8f));

        private CursorKind Decide(Entity hovered)
        {
            SetSingleton(new HoveredEntity { Value = hovered });
            _intent.Update(_world.World.Unmanaged);
            return _world.Singleton<CursorIntent>().Kind;
        }

        private void SetSingleton<T>(T value) where T : unmanaged, IComponentData
        {
            using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            query.SetSingleton(value);
        }

        private void Select(Entity entity) => _world.EntityManager.SetComponentEnabled<Selected>(entity, true);

        private Entity Tank(float3 position) => _world.AddWeapon(_world.SpawnUnit(1, position));

        private Entity Engineer(float3 position)
        {
            var engineer = _world.SpawnUnit(1, position);
            var writer = new EntityManagerWriter(_world.EntityManager, engineer);
            CaptureSetup.AddCapturer(ref writer, 1f, consumedOnCapture: false);
            return engineer;
        }

        private Entity Post()
        {
            var post = _world.SpawnBuilding(Faction.Neutral, new float3(10f, 0f, 0f), Footprint);
            var writer = new EntityManagerWriter(_world.EntityManager, post);
            CaptureSetup.AddCapturable(ref writer, 2f);
            return post;
        }
    }
}
