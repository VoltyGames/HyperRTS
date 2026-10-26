using System.IO;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Replays;
using HyperRTS.Simulation.Tests.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Replays
{
    /// <summary>Replays keep cues: recorded with the sample after them, saved and loaded, queued again in playback.</summary>
    public class ReplayImpactTests
    {
        private static readonly float3 TargetPosition = new(3f, 0f, 0f);

        private ImpactMark _mark;
        private SoundCue _sound;

        [SetUp]
        public void SetUp()
        {
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _sound = ScriptableObject.CreateInstance<SoundCue>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mark);
            Object.DestroyImmediate(_sound);
        }

        /// <summary>
        /// 1.5 s at 10 Hz: a "Target" walks into range at 0.5 s and a "Mortar" fires at it once, with a fire sound
        /// when <paramref name="loud"/>.
        /// </summary>
        private Replay Record(bool loud = false)
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var gun = world.AddWeapon(world.SpawnUnit(1, float3.zero, name: "Mortar"), range: 6f, cooldown: 100f);
            Combat.ImpactTests.Marked(world, gun, _mark);
            if (loud)
            {
                var writer = new EntityManagerWriter(world.EntityManager, gun);
                EntitySoundsAuthoring.Set(EntitySoundsAuthoring.Add(ref writer), CueSlot.Fire, _sound);
            }

            ReplayRecorder.Start(world.World, sampleRate: 10f, keyframeInterval: 0.5f);
            world.Run(0.5f);
            world.SpawnUnit(2, TargetPosition, maxHealth: 1000f, name: "Target");
            world.Run(1f);
            return ReplayRecorder.Stop(world.World, "Assets/Maps/Test.unity");
        }

        private static TestWorld Viewer(Replay replay)
        {
            var world = new TestWorld();
            world.CreateMatch(1, 2);
            world.SpawnUnit(1, float3.zero, name: "Mortar");
            world.MakePrefab(world.SpawnUnit(2, float3.zero, maxHealth: 1000f, name: "Target"));
            ReplayViewer.Begin(world.World, replay);
            return world;
        }

        private static int QueuedAfterSeek(TestWorld world, float time)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(ReplayPlayback));
            var clock = query.GetSingleton<ReplayPlayback>();
            clock.Playing = false;
            clock.Time = time;
            query.SetSingleton(clock);
            world.Tick();
            return world.Queued(CueSlot.Impact).Length;
        }

        [Test]
        public void Recorder_KeepsEachImpact_WithTheSampleAfterIt()
        {
            var replay = Record();

            var total = 0;
            foreach (var frame in replay.Frames)
            {
                Assert.AreEqual(total, frame.CueStart, "frames hold consecutive cues");
                total += frame.CueCount;
            }

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, replay.Cues.Length);
            var impact = replay.Cues[0].Unpack();
            Assert.AreEqual(EntityInfo.TypeIdFromName("Mortar"), impact.TypeId);
            Assert.AreEqual(TargetPosition.x, impact.Position.x, QuantizedPosition.Step);
            Assert.AreEqual(ImpactSurface.Ground, impact.Surface);
            Assert.AreEqual(1, impact.Faction);
        }

        [Test]
        public void Serializer_RoundTripsCues()
        {
            var replay = Record();
            replay.Cues[0].Detail = (byte)SurfaceMaterial.Metal;

            using var memory = new MemoryStream();
            ReplaySerializer.Write(replay, memory);
            memory.Position = 0;
            var read = ReplaySerializer.Read(memory);

            CollectionAssert.AreEqual(replay.Frames, read.Frames);
            CollectionAssert.AreEqual(replay.Cues, read.Cues);
        }

        [Test]
        public void Playback_QueuesEachImpactOnce_AndAJumpQueuesTheRecentOnes()
        {
            var replay = Record();
            using var world = Viewer(replay);

            var queued = 0;
            for (var t = 0f; t < replay.Duration + 0.5f; t += TestWorld.FrameTime)
            {
                world.Tick();
                queued += world.Queued(CueSlot.Impact).Length;
            }

            Assert.AreEqual(1, queued, "played through once");
            Assert.AreEqual(0, QueuedAfterSeek(world, 0.1f), "back before the shot");
            Assert.AreEqual(1, QueuedAfterSeek(world, replay.Duration), "a jump past it shows its mark");
            Assert.AreEqual(0, QueuedAfterSeek(world, replay.Duration), "staying put queues nothing");
        }

        [Test]
        public void Playback_QueuesTheFireCue_WithTheImpact()
        {
            var replay = Record(loud: true);
            using var world = Viewer(replay);

            var fired = 0;
            for (var t = 0f; t < replay.Duration + 0.5f; t += TestWorld.FrameTime)
            {
                world.Tick();
                fired += world.Queued(CueSlot.Fire).Length;
            }

            Assert.AreEqual(2, replay.Cues.Length, "the shot and where it landed");
            Assert.AreEqual(1, fired, "the shot plays again once");
        }
    }
}
