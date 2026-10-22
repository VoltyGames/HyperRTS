using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Machines run always, while their building is powered or while it produces, easing in and out.</summary>
    public class MachineRunTests
    {
        private const float Step = 0.1f;

        private World _world;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Machine Run Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<MachineSystem>();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Always_RunsAtFullSpeedFromTheFirstFrame()
        {
            var machine = Machine(MachineRun.Always, Building());

            Run(1f);

            Assert.AreEqual(1f, Clock(machine).Time, 1e-4f);
        }

        [Test]
        public void Powered_EasesToAStopOffline_AndBackOnline()
        {
            var building = Building();
            var machine = Machine(MachineRun.Powered, building);
            Run(1f);
            Assert.AreEqual(1f, Clock(machine).Time, 1e-4f, "starts at full speed when first seen powered");

            SetOffline(building, OfflineReason.Unpowered);
            var before = Clock(machine).Time;
            Run(1f);
            var spunDown = Clock(machine).Time - before;
            Assert.Greater(spunDown, 0f, "coasts rather than snapping still");
            Assert.Less(spunDown, 1f);

            before = Clock(machine).Time;
            Run(1f);
            Assert.AreEqual(before, Clock(machine).Time, 1e-5f, "stopped");

            SetOffline(building, OfflineReason.None);
            Run(Step);
            var step = Clock(machine).Time - before;
            Assert.Greater(step, 0f);
            Assert.Less(step, Step, "spins back up gradually");
        }

        [Test]
        public void Powered_StandsStillUnderConstruction()
        {
            var building = Building();
            SetOffline(building, OfflineReason.UnderConstruction);
            var machine = Machine(MachineRun.Powered, building);

            Run(1f);

            Assert.AreEqual(0f, Clock(machine).Time);
        }

        [Test]
        public void Producing_RunsOnlyWhileTheQueueHasWork()
        {
            var building = Building();
            var machine = Machine(MachineRun.Producing, building);
            Run(1f);
            Assert.AreEqual(0f, Clock(machine).Time, "idle factory");

            _em.GetBuffer<ProductionQueueItem>(building).Add(new ProductionQueueItem { TypeId = 1 });
            Run(2f);
            Assert.Greater(Clock(machine).Time, 1f);

            SetOffline(building, OfflineReason.Unpowered);
            var before = Clock(machine).Time;
            Run(Step);
            Assert.AreEqual(before + Step, Clock(machine).Time, 1e-4f, "low power only slows production");

            SetOffline(building, OfflineReason.Custom);
            Run(2f);
            before = Clock(machine).Time;
            Run(1f);
            Assert.AreEqual(before, Clock(machine).Time, 1e-5f, "halted production stops it");
        }

        [Test]
        public void Slide_TravelsToAndFroAlongItsAxis()
        {
            var machine = Machine(MachineRun.Always, Building());
            var crane = _em.CreateEntity();
            _em.AddComponentData(crane, LocalTransform.FromPosition(new float3(0f, 5f, 0f)));
            _em.AddComponentData(crane, new Parent { Value = machine });
            _em.AddComponentData(crane, new MachinePart
            {
                Machine = machine, Motion = MachineMotion.Slide, Axis = math.right(), Speed = 90f, Amplitude = 3f,
                Rest = quaternion.identity, RestPosition = new float3(0f, 5f, 0f),
            });

            Run(1f);

            Assert.AreEqual(3f, _em.GetComponentData<LocalTransform>(crane).Position.x, 1e-3f);
            Assert.AreEqual(5f, _em.GetComponentData<LocalTransform>(crane).Position.y, 1e-4f);
        }

        private Entity Building()
        {
            var building = _em.CreateEntity();
            _em.AddComponentData(building, new Offline());
            _em.SetComponentEnabled<Offline>(building, false);
            _em.AddBuffer<ProductionQueueItem>(building);
            return building;
        }

        private Entity Machine(MachineRun run, Entity building)
        {
            var machine = _em.CreateEntity();
            _em.AddComponentData(machine, new MachineClock { Run = run, Building = building, RampSeconds = 1f });
            return machine;
        }

        private void SetOffline(Entity building, OfflineReason reasons)
        {
            _em.SetComponentData(building, new Offline { Reasons = reasons });
            _em.SetComponentEnabled<Offline>(building, reasons != OfflineReason.None);
        }

        private MachineClock Clock(Entity machine) => _em.GetComponentData<MachineClock>(machine);

        private void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Step)
            {
                _world.SetTime(new TimeData(_world.Time.ElapsedTime + Step, Step));
                _system.Update(_world.Unmanaged);
                _em.CompleteAllTrackedJobs();
            }
        }
    }
}
