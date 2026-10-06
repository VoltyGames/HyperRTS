using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Replay playback in a fresh world: scene takeover, interpolation, spawns and removals, seeking, speed.</summary>
    public class ReplayPlaybackTests
    {
        private const float Tolerance = 0.15f;

        private TestWorld _world;
        private List<(float Time, float3 Position)> _track;
        private Entity _mover;
        private Entity _idle;

        [SetUp]
        public void SetUp()
        {
            var replay = ReplayTests.Record(out _track);

            // The map as the replay's scene bakes it: starting entities placed, spawnable types as prefabs.
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _mover = _world.SpawnUnit(1, ReplayTests.MoverStart, name: "Mover");
            _idle = _world.SpawnUnit(1, ReplayTests.IdlePosition, name: "Idle");
            _world.SpawnUnit(2, ReplayTests.VictimPosition, name: "Victim");
            _world.MakePrefab(_world.SpawnUnit(1, float3.zero, name: "Tank"));
            _world.MakePrefab(_world.SpawnUnit(2, float3.zero, name: "Victim"));
            ReplayViewer.Begin(_world.World, replay);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private ReplayPlayback Clock
        {
            get
            {
                using var query = _world.EntityManager.CreateEntityQuery(typeof(ReplayPlayback));
                return query.GetSingleton<ReplayPlayback>();
            }
            set
            {
                using var query = _world.EntityManager.CreateEntityQuery(typeof(ReplayPlayback));
                query.SetSingleton(value);
            }
        }

        private void SeekTo(float time)
        {
            var clock = Clock;
            clock.Playing = false;
            clock.Time = time;
            Clock = clock;
            _world.Tick();
        }

        private int Count(string name)
        {
            var typeId = EntityInfo.TypeIdFromName(name);
            using var query = _world.EntityManager.CreateEntityQuery(typeof(EntityInfo));
            using var infos = query.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            var count = 0;
            foreach (var info in infos)
            {
                count += info.TypeId == typeId ? 1 : 0;
            }

            return count;
        }

        private void AssertMoverAt(int trackIndex)
        {
            SeekTo(_track[trackIndex].Time);
            var position = _world.Get<LocalTransform>(_mover).Position;
            Assert.AreEqual(0f, math.distance(_track[trackIndex].Position, position), Tolerance,
                $"mover at {_track[trackIndex].Time:0.00}s");
        }

        [Test]
        public void Playback_TakesOverScene_AndReproducesPositions()
        {
            AssertMoverAt(16);

            Assert.IsTrue(_world.EntityManager.Exists(_idle), "scene entities are reused, not replaced");
            Assert.AreEqual(ReplayTests.IdlePosition, _world.Get<LocalTransform>(_idle).Position);
            Assert.AreEqual(0, Count("Victim"), "the victim died at 0.2 s");
            Assert.IsFalse(_world.World.GetExistingSystemManaged<CombatSystemGroup>().Enabled, "no gameplay");

            var before = _world.Get<LocalTransform>(_mover).Position;
            _world.Tick(frames: 10);
            Assert.AreEqual(before, _world.Get<LocalTransform>(_mover).Position, "paused playback holds still");
        }

        [Test]
        public void Seek_ForwardAndBack_SpawnsAndRemoves()
        {
            AssertMoverAt(40);
            Assert.AreEqual(0, Count("Victim"), "the victim died at 0.2 s");
            Assert.AreEqual(1, Count("Tank"), "the tank spawned at 0.67 s from its prefab");

            AssertMoverAt(3);
            Assert.AreEqual(0, Count("Tank"));
            Assert.AreEqual(1, Count("Victim"), "seeking back brings the victim back from its prefab");
            Assert.AreEqual(ReplayTests.VictimPosition, FirstPosition("Victim"));

            AssertMoverAt(25);
            Assert.AreEqual(ReplayTests.TankPosition, FirstPosition("Tank"));
        }

        [Test]
        public void Playing_AdvancesAtSpeed_AndStopsAtTheEnd()
        {
            SeekTo(0f);
            var clock = Clock;
            clock.Playing = true;
            clock.Speed = 2f;
            Clock = clock;

            _world.Tick(frames: 6);
            Assert.AreEqual(12f * TestWorld.FrameTime, Clock.Time, 1e-3f);

            _world.Tick(frames: 30);
            Assert.AreEqual(Clock.Duration, Clock.Time);
            Assert.IsFalse(Clock.Playing);

            ReplayViewer.Stop(_world.World);
            Assert.IsFalse(ReplayViewer.IsPlaying(_world.World));
            Assert.IsTrue(_world.World.GetExistingSystemManaged<CombatSystemGroup>().Enabled);
        }

        [Test]
        public void Stop_LeavesGameplayAsItWasBeforePlayback()
        {
            var replay = ReplayTests.Record(out _);
            var combat = _world.World.GetExistingSystemManaged<CombatSystemGroup>();
            ReplayViewer.Stop(_world.World);
            combat.Enabled = false;

            ReplayViewer.Begin(_world.World, replay);
            Assert.IsFalse(_world.World.GetExistingSystemManaged<OrderSystemGroup>().Enabled, "paused for playback");
            ReplayViewer.Stop(_world.World);

            Assert.IsTrue(_world.World.GetExistingSystemManaged<OrderSystemGroup>().Enabled);
            Assert.IsFalse(combat.Enabled, "a phase paused before playback stays paused");
        }

        private float3 FirstPosition(string name)
        {
            var typeId = EntityInfo.TypeIdFromName(name);
            using var query = _world.EntityManager.CreateEntityQuery(typeof(EntityInfo), typeof(LocalTransform));
            using var infos = query.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < infos.Length; i++)
            {
                if (infos[i].TypeId == typeId)
                {
                    return transforms[i].Position;
                }
            }

            throw new AssertionException($"No {name} in the world.");
        }
    }
}
