using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Audio
{
    /// <summary>Looping sounds: how an entity's loops follow its motion, and which loops win the mixer's sources.</summary>
    public class LoopSoundTests
    {
        private const float Frame = 1f / 60f;

        private static readonly LoopSoundTuning Tuning = new() { FullSpeed = 10f, PitchRise = 0.2f };

        private SoundCue _engine;
        private SoundCue _hum;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("Loop", 100, 1, 1000, false);
            _engine = Cue(2, 50f);
            _hum = Cue(4, 20f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_engine);
            Object.DestroyImmediate(_hum);
            Object.DestroyImmediate(_clip);
        }

        [TestCase(0f, 1f, 0f, 1f)]
        [TestCase(5f, 0.5f, 0.5f, 1.1f)]
        [TestCase(10f, 0f, 1f, 1.2f)]
        public void Engine_CrossfadesFromIdleToMove_AndPitchesUpWithSpeed(float speed, float idle, float move,
            float pitch)
        {
            var state = Ran(3f, speed);

            Assert.AreEqual(idle, state.Level(LoopSlot.EngineIdle, Tuning, false), 0.02f);
            Assert.AreEqual(move, state.Level(LoopSlot.EngineMove, Tuning, false), 0.02f);
            Assert.AreEqual(pitch, state.PitchScale(LoopSlot.EngineMove, Tuning), 0.01f);
        }

        [Test]
        public void Rotor_FollowsTheBladesSpin()
        {
            var state = Ran(0.1f, 0f, rotor: 0.5f);
            Assert.AreEqual(0.5f, state.Level(LoopSlot.Rotor, Tuning, false));
            Assert.AreEqual(0.8f, state.PitchScale(LoopSlot.Rotor, Tuning), 1e-5f);

            Run(ref state, 0.1f, 0.1f, 0f, rotor: 1f);
            Assert.AreEqual(1f, state.Level(LoopSlot.Rotor, Tuning, false));
            Assert.AreEqual(1f, state.PitchScale(LoopSlot.Rotor, Tuning));

            Run(ref state, 0.2f, 0.1f, 0f);
            Assert.AreEqual(0f, state.Level(LoopSlot.Rotor, Tuning, false));
        }

        [Test]
        public void Traverse_HoldsBetweenSnapshots_ThenFades()
        {
            var state = new LoopSoundState();
            Run(ref state, 0f, Frame, 0f);
            Run(ref state, Frame, 9f * Frame, 0f, aim: 5);
            Assert.AreEqual(1f, state.Level(LoopSlot.TurretTraverse, Tuning, false), "still turning between updates");

            Run(ref state, 10f * Frame, 1f, 0f, aim: 5);
            Assert.AreEqual(0f, state.Level(LoopSlot.TurretTraverse, Tuning, false));
        }

        [Test]
        public void Working_FollowsTheFlag_AmbientAlwaysPlays()
        {
            var state = Ran(0.1f, 0f);

            Assert.AreEqual(1f, state.Level(LoopSlot.Working, Tuning, true));
            Assert.AreEqual(0f, state.Level(LoopSlot.Working, Tuning, false));
            Assert.AreEqual(1f, state.Level(LoopSlot.Ambient, Tuning, false));
        }

        [Test]
        public void ManyOfOneCue_OnlyTheNearestWithinItsLimitPlay()
        {
            var mixer = Mixed(8, Loop(1, _engine, 30f), Loop(2, _engine, 10f), Loop(3, _engine, 20f));

            Assert.AreEqual(2, mixer.Winners.Count);
            Assert.AreEqual(2, mixer.Winners[0].Loop.Entity.Index);
            Assert.AreEqual(3, mixer.Winners[1].Loop.Entity.Index);
        }

        [Test]
        public void OutOfRange_AndSilentLoops_DontPlay()
        {
            var mixer = Mixed(8, Loop(1, _hum, 25f), Loop(2, _hum, 5f, level: 0f));

            Assert.IsEmpty(mixer.Winners);
        }

        [Test]
        public void FullMixer_KeepsTheNearestOverall()
        {
            var mixer = Mixed(2, Loop(1, _hum, 15f), Loop(2, _engine, 5f), Loop(3, _hum, 1f));

            CollectionAssert.AreEquivalent(new[] { 3, 2 },
                new[] { mixer.Winners[0].Loop.Entity.Index, mixer.Winners[1].Loop.Entity.Index });
        }

        [Test]
        public void PlayingLoop_KeepsItsSource_AndALoserFreesIt()
        {
            var mixer = Mixed(4, Loop(1, _engine, 10f));
            var source = mixer.Winners[0].Source;
            Assert.IsTrue(mixer.Winners[0].Claimed);

            Mix(mixer, Loop(2, _engine, 40f), Loop(1, _engine, 12f));
            Assert.AreEqual(source, mixer.Winners[0].Source);
            Assert.IsFalse(mixer.Winners[0].Claimed, "a kept loop doesn't restart");
            Assert.IsTrue(mixer.Winners[1].Claimed);
            Assert.AreNotEqual(source, mixer.Winners[1].Source);

            Mix(mixer, Loop(2, _engine, 40f));
            CollectionAssert.AreEqual(new[] { source }, mixer.Released);
            Assert.IsFalse(mixer.Winners[0].Claimed);
        }

        private static void Run(ref LoopSoundState state, float from, float seconds, float speed, float rotor = 0f,
            uint aim = 0)
        {
            var position = state.LastPosition;
            for (var t = 0f; t < seconds; t += Frame)
            {
                position.x += speed * Frame;
                state.Step(position, rotor, aim, from + t, Frame);
            }
        }

        private static LoopSoundState Ran(float seconds, float speed, float rotor = 0f)
        {
            var state = new LoopSoundState();
            Run(ref state, 0f, seconds, speed, rotor);
            return state;
        }

        private SoundCue Cue(int maxInstances, float maxDistance)
        {
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.clips = new[] { _clip };
            cue.maxInstances = maxInstances;
            cue.maxDistance = maxDistance;
            return cue;
        }

        private static LoopCandidate Loop(int entity, SoundCue cue, float distance, float level = 1f) => new()
        {
            Entity = new Entity { Index = entity, Version = 1 }, Slot = LoopSlot.EngineIdle, Cue = cue,
            Level = level, PitchScale = 1f, DistanceSq = distance * distance,
        };

        private static LoopMixer Mixed(int sources, params LoopCandidate[] loops)
        {
            var mixer = new LoopMixer(sources);
            Mix(mixer, loops);
            return mixer;
        }

        private static void Mix(LoopMixer mixer, params LoopCandidate[] loops)
        {
            mixer.Begin();
            foreach (var loop in loops)
            {
                mixer.Offer(loop);
            }

            mixer.Resolve();
        }
    }
}
