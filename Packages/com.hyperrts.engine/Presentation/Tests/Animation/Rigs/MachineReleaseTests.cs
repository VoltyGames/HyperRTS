using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Rigs
{
    /// <summary>
    /// Door machines open per released unit, hold, close, roll up by scale, skip releases from before first sight, and
    /// stay open through an aircraft's drop run.
    /// </summary>
    public class MachineReleaseTests
    {
        private const float Step = 0.1f;
        private static readonly float3 Rest = new(0f, 1f, 0f);
        private static readonly float3 Lift = new(0f, 4f, 0f);

        private World _world;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Machine Release Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<MachineSystem>();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Release_OpensWithEasing_HoldsThenCloses()
        {
            var building = Building(0);
            var door = Door(building, MachineRun.Releasing, MachineMotion.Extend);
            Run(1f);
            Assert.AreEqual(Rest, Position(door), "shut before any release");

            Release(building);
            Run(0.5f);
            Assert.That(Position(door).y, Is.GreaterThan(Rest.y).And.LessThan(Rest.y + Lift.y));

            Run(1f);
            Assert.AreEqual(Rest.y + Lift.y, Position(door).y, 1e-4f, "open");

            Run(2f);
            Assert.AreEqual(Rest.y + Lift.y, Position(door).y, 1e-4f, "holds while the unit drives out");

            Run(3f);
            Assert.AreEqual(Rest.y, Position(door).y, 1e-4f, "closed again");
        }

        [Test]
        public void ReleaseWhileClosing_ReopensFromWhereItStands()
        {
            var building = Building(0);
            var door = Door(building, MachineRun.Releasing, MachineMotion.Extend);
            Run(Step);
            Release(building);
            Run(4.5f);
            var closing = Position(door).y;
            Assert.Less(closing, Rest.y + Lift.y);

            Release(building);
            Run(Step);
            Assert.Greater(Position(door).y, closing, "turns back up without snapping shut");
        }

        [Test]
        public void BuildingFirstSeenMidMatch_DoesNotReplayOldReleases()
        {
            var door = Door(Building(7), MachineRun.Releasing, MachineMotion.Extend);

            Run(1f);

            Assert.AreEqual(Rest, Position(door));
        }

        [Test]
        public void RollUpDoor_ScalesItsHeightTowardThePivot_AndBack()
        {
            var building = Building(0);
            var rest = new float3(2f, 3f, 1f);
            var door = Door(building, MachineRun.Releasing, MachineMotion.Stretch);
            var part = _em.GetComponentData<MachinePart>(door);
            part.OpenScale = new float3(1f, 0.05f, 1f);
            part.RestScale = rest;
            _em.SetComponentData(door, part);
            _em.AddComponentData(door, new PostTransformMatrix { Value = float4x4.Scale(rest) });
            Run(Step);

            Release(building);
            Run(0.5f);
            Assert.That(Height(door), Is.LessThan(rest.y).And.GreaterThan(rest.y * 0.05f), "eases rather than snapping");

            Run(1f);
            Assert.AreEqual(rest.y * 0.05f, Height(door), 1e-4f, "rolled up");
            Assert.AreEqual(rest.x, _em.GetComponentData<PostTransformMatrix>(door).Value.c0.x, 1e-4f, "width kept");
            Assert.AreEqual(Rest, Position(door), "rolls in place");

            Run(5f);
            Assert.AreEqual(rest.y, Height(door), 1e-4f, "rolled back down");
        }

        [Test]
        public void DropRun_HoldsTheAircraftsDoorOpenThroughIt_ThenCloses()
        {
            var aircraft = _em.CreateEntity();
            _em.AddComponentData(aircraft, new DropRun());
            var door = Door(aircraft, MachineRun.Dropping, MachineMotion.Extend);
            Run(1f);
            Assert.AreEqual(Rest, Position(door), "shut before the run");

            _em.SetComponentData(aircraft, new DropRun { Active = true });
            Run(0.5f);
            Assert.Greater(Position(door).y, Rest.y, "eases open");
            Run(6f);
            Assert.AreEqual(Rest.y + Lift.y, Position(door).y, 1e-4f, "stays open while the run lasts");

            _em.SetComponentData(aircraft, new DropRun());
            Run(2f);
            Assert.AreEqual(Rest.y, Position(door).y, 1e-4f, "closed after the run");
        }

        private float Height(Entity door) => _em.GetComponentData<PostTransformMatrix>(door).Value.c1.y;

        private void Release(Entity building)
        {
            var producer = _em.GetComponentData<Producer>(building);
            producer.Released++;
            _em.SetComponentData(building, producer);
        }

        private Entity Building(uint released)
        {
            var building = _em.CreateEntity();
            _em.AddComponentData(building, new Producer { Released = released });
            return building;
        }

        // A door is its own machine: one second to move, and three more open after the release.
        private Entity Door(Entity building, MachineRun run, MachineMotion motion)
        {
            var door = _em.CreateEntity();
            _em.AddComponentData(door, LocalTransform.FromPosition(Rest));
            _em.AddComponentData(door, new Parent { Value = building });
            _em.AddComponentData(door, new MachineClock { Run = run, Owner = building, RampSeconds = 1f, KeepRunningSeconds = 4f });
            _em.AddComponentData(door, new MachinePart
            {
                Machine = door,
                Motion = motion,
                Axis = math.up(),
                Amplitude = Lift.y,
                To = 1f,
                Rest = quaternion.identity,
                RestPosition = Rest,
                RestScale = new float3(1f),
                OpenScale = new float3(1f),
            });
            return door;
        }

        private float3 Position(Entity door) => _em.GetComponentData<LocalTransform>(door).Position;

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
