using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>
    /// Machines run always, while their building is powered, produces or fires, or as their unit deploys, easing in
    /// and out, and their parts move.
    /// </summary>
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
        public void Firing_RunsForASecondAfterEachShot()
        {
            var building = Building();
            var poses = _em.AddBuffer<WeaponPose>(building);
            poses.Add(new WeaponPose { Shots = 7 });
            var machine = Machine(MachineRun.Firing, building);
            Run(1f);
            Assert.AreEqual(0f, Clock(machine).Time, "a count already there when first seen is no shot");

            var shot = _em.GetBuffer<WeaponPose>(building);
            var pose = shot[0];
            pose.CountShot();
            shot[0] = pose;
            Run(0.5f);
            Assert.Greater(Clock(machine).Time, 0f, "spins up on a shot");

            Run(MachineClock.FiringSeconds + 1f);
            var before = Clock(machine).Time;
            Run(1f);
            Assert.AreEqual(before, Clock(machine).Time, 1e-5f, "stopped once the shots end");
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

        [Test]
        public void Deployed_UnfoldsEachPartOverItsOwnSpan()
        {
            var unit = _em.CreateEntity();
            _em.AddComponentData(unit, new Deployable { DeployTime = 1f, PackUpTime = 1f, Progress = 0.5f });
            var machine = Machine(MachineRun.Deployed, unit);
            var jack = Part(machine, new MachinePart { Motion = MachineMotion.Extend, Axis = math.down(), Amplitude = 0.5f, From = 0f, To = 0.5f });
            var launcher = Part(machine, new MachinePart { Motion = MachineMotion.Raise, Axis = math.right(), Amplitude = 40f, From = 0.5f, To = 1f });

            Run(Step);
            Assert.AreEqual(-0.5f, _em.GetComponentData<LocalTransform>(jack).Position.y, 1e-4f, "the jacks are down first");
            Assert.AreEqual(quaternion.identity, _em.GetComponentData<LocalTransform>(launcher).Rotation, "the launcher waits");

            _em.SetComponentData(unit, new Deployable { DeployTime = 1f, PackUpTime = 1f, Progress = 1f });
            Run(Step);
            var raised = math.rotate(_em.GetComponentData<LocalTransform>(launcher).Rotation, math.forward());
            Assert.AreEqual(math.sin(math.radians(-40f)), raised.y, 1e-4f, "then it raises fully");
        }

        [Test]
        public void Point_TurnsAboutItsAxisToKeepAimingAtItsTarget()
        {
            var ram = new MachinePart
            {
                Motion = MachineMotion.Point, Axis = math.right(), Rest = quaternion.identity, RestAim = math.forward(),
            };

            var pointing = math.rotate(ram.Pointing(new float3(3f, 2f, 2f)), math.forward());

            Assert.Less(math.distance(math.normalize(new float3(0f, 1f, 1f)), pointing), 1e-4f, "turns only about its axis");
            Assert.AreEqual(quaternion.identity, ram.Pointing(new float3(0f, 0f, 4f)), "at its rest bearing it rests");
        }

        [Test]
        public void Downwind_TurnsAboutItsAxisToPointWithTheWind()
        {
            var sock = new MachinePart { Motion = MachineMotion.Downwind, Axis = math.up(), Rest = quaternion.identity };

            var pointing = math.rotate(sock.FacingDownwind(new float3(1f, 0.3f, 0f)), math.forward());

            Assert.AreEqual(1f, pointing.x, 1e-4f);
            Assert.AreEqual(0f, pointing.y, 1e-4f, "stays level");
            Assert.AreEqual(quaternion.identity, sock.FacingDownwind(float3.zero), "no wind keeps the rest pose");
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
            _em.AddComponentData(machine, new MachineClock { Run = run, Owner = building, RampSeconds = 1f });
            return machine;
        }

        private Entity Part(Entity machine, MachinePart part)
        {
            var entity = _em.CreateEntity();
            part.Machine = machine;
            part.Rest = quaternion.identity;
            _em.AddComponentData(entity, LocalTransform.Identity);
            _em.AddComponentData(entity, new Parent { Value = machine });
            _em.AddComponentData(entity, part);
            return entity;
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
