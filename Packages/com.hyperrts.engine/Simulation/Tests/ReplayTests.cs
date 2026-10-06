using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Replays: recording keyframes and deltas, the file format, playback, seeking.</summary>
    public class ReplayTests
    {
        internal static readonly float3 MoverStart = new(0f, 0f, 0f);
        internal static readonly float3 IdlePosition = new(-10f, 0f, 10f);
        internal static readonly float3 VictimPosition = new(10f, 0f, -10f);
        internal static readonly float3 TankPosition = new(-20f, 0f, -20f);

        /// <summary>
        /// 1.5 s at 10 Hz with a keyframe every 0.5 s: "Mover" drives east, "Idle" waits, "Victim" dies at frame 6
        /// and a "Tank" spawns at frame 20. <paramref name="track"/> is the mover's position per recording time.
        /// </summary>
        internal static Replay Record(out List<(float Time, float3 Position)> track)
        {
            track = new List<(float, float3)>();
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var mover = world.SpawnUnit(1, MoverStart, name: "Mover");
            world.SpawnUnit(1, IdlePosition, name: "Idle");
            var victim = world.SpawnUnit(2, VictimPosition, name: "Victim");
            world.EntityManager.SetComponentData(mover, new MoveDestination { Value = new float3(30f, 0f, 0f) });
            world.EntityManager.SetComponentEnabled<MoveDestination>(mover, true);
            ReplayRecorder.Start(world.World, sampleRate: 10f, keyframeInterval: 0.5f);

            for (var frame = 0; frame < 45; frame++)
            {
                if (frame == 6)
                {
                    world.EntityManager.SetComponentData(victim, new Health { Current = 0f, Max = 100f });
                }

                if (frame == 20)
                {
                    world.SpawnUnit(1, TankPosition, name: "Tank");
                }

                world.Tick();
                track.Add((frame * TestWorld.FrameTime, world.Get<LocalTransform>(mover).Position));
            }

            return ReplayRecorder.Stop(world.World);
        }

        private static int KeyOf(Replay replay, string name)
        {
            var typeId = EntityInfo.TypeIdFromName(name);
            foreach (var entity in replay.Entities)
            {
                if (entity.TypeId == typeId)
                {
                    return entity.Key;
                }
            }

            return -1;
        }

        [Test]
        public void Recorder_WritesKeyframes_AndOnlyChangesInDeltas()
        {
            var replay = Record(out _);
            var mover = KeyOf(replay, "Mover");

            Assert.IsTrue(replay.Frames[0].Keyframe);
            Assert.AreEqual(3, replay.Frames[0].EntityCount, "the first keyframe holds every entity");
            Assert.AreEqual(1.4f, replay.Duration, 0.05f);
            Assert.AreEqual(2, replay.Players.Count);
            Assert.AreEqual(2, replay.Players[1].Team);

            var delta = replay.Frames[1];
            Assert.IsFalse(delta.Keyframe);
            Assert.AreEqual(1, delta.EntityCount, "idle entities are left out of deltas");
            Assert.AreEqual(mover, replay.Entities[delta.EntityStart].Key);

            CollectionAssert.Contains(replay.Removed, KeyOf(replay, "Victim"));
            Assert.AreEqual(3, KeyOf(replay, "Tank"), "spawns get the next key");

            var last = Array.FindLast(replay.Frames, frame => frame.Keyframe);
            Assert.AreEqual(3, last.EntityCount, "a later keyframe holds the living: mover, idle and tank");
        }

        [Test]
        public void Serializer_RoundTrips()
        {
            var replay = Record(out _);
            replay.ScenePath = "Assets/Maps/Test.unity";
            replay.Result = new MatchState { Phase = MatchPhase.Ended, WinningTeam = 2 };

            using var memory = new MemoryStream();
            ReplaySerializer.Write(replay, memory);
            memory.Position = 0;
            var read = ReplaySerializer.Read(memory);

            Assert.AreEqual(replay.ScenePath, read.ScenePath);
            Assert.AreEqual(replay.SampleRate, read.SampleRate);
            Assert.AreEqual(replay.Duration, read.Duration);
            Assert.AreEqual(MatchPhase.Ended, read.Result.Phase);
            Assert.AreEqual(2, read.Result.WinningTeam);
            Assert.AreEqual(replay.Players.Count, read.Players.Count);
            Assert.AreEqual(replay.Players[0].Name, read.Players[0].Name);
            Assert.AreEqual(replay.Players[1].Color, read.Players[1].Color);
            CollectionAssert.AreEqual(replay.Frames, read.Frames);
            CollectionAssert.AreEqual(replay.Entities, read.Entities);
            CollectionAssert.AreEqual(replay.Removed, read.Removed);
        }

        [Test]
        public void Entity_QuantizesPosition_AndTurnsTheShortWay()
        {
            var a = ReplayEntity.Capture(0, 0, 1,
                LocalTransform.FromPositionRotation(new float3(1.03f, 0f, -2.5f), quaternion.RotateY(math.radians(350f))),
                0.5f, 0f, false);
            var b = ReplayEntity.Capture(0, 0, 1,
                LocalTransform.FromPositionRotation(new float3(3.03f, 0f, -2.5f), quaternion.RotateY(math.radians(10f))),
                0.5f, 0f, false);

            Assert.AreEqual(1.03f, a.Position.x, ReplayEntity.PositionStep);
            Assert.AreEqual(0.5f, a.HealthFraction, 0.01f);
            Assert.IsFalse(a.UnderConstruction);

            var middle = a.Interpolate(b, 0.5f, 1f);
            var forward = math.mul(middle.Rotation, math.forward());
            Assert.AreEqual(2.03f, middle.Position.x, ReplayEntity.PositionStep);
            Assert.AreEqual(1f, forward.z, 1e-3f, "350° to 10° passes through 0°, not 180°");
        }

        [Test]
        public void Seek_FromKeyframe_MatchesSteppingEveryFrame()
        {
            var replay = Record(out _);
            var stream = replay.ToStream(Allocator.Persistent);
            var stepped = new NativeHashMap<int, ReplayEntity>(8, Allocator.Persistent);
            var sought = new NativeHashMap<int, ReplayEntity>(8, Allocator.Persistent);
            try
            {
                Assert.GreaterOrEqual(stream.KeyframeAtOrBefore(stream.Frames.Length - 1), 10, "keyframes recur");
                for (var i = 0; i < stream.Frames.Length; i++)
                {
                    stream.Apply(i, stepped);
                    sought.Clear();
                    for (var k = stream.KeyframeAtOrBefore(i); k <= i; k++)
                    {
                        stream.Apply(k, sought);
                    }

                    Assert.AreEqual(stepped.Count, sought.Count, $"frame {i}");
                    foreach (var pair in stepped)
                    {
                        Assert.IsTrue(sought[pair.Key].SameState(pair.Value), $"frame {i}, key {pair.Key}");
                    }
                }
            }
            finally
            {
                stream.Dispose();
                stepped.Dispose();
                sought.Dispose();
            }
        }

        [Test]
        public void MatchRules_RecordReplay_StartsRecordingByItself()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            using var query = world.EntityManager.CreateEntityQuery(typeof(MatchRules));
            var rules = query.GetSingleton<MatchRules>();
            rules.RecordReplay = true;
            query.SetSingleton(rules);

            world.Tick();

            Assert.IsTrue(ReplayRecorder.IsRecording(world.World));
        }
    }
}
