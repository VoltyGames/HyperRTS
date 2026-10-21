using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>How an entity's loops follow its speed, flight and turret turns.</summary>
    public class LoopSoundStateTests
    {
        private const float Frame = 1f / 60f;

        private static readonly LoopSoundTuning Tuning = new() { FullSpeed = 10f, PitchRise = 0.2f };

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

        [Test]
        public void Engine_CrossfadesFromIdleToMove_AndPitchesUpWithSpeed()
        {
            var parked = Ran(3f, 0f);
            var cruising = Ran(3f, 10f);

            Assert.AreEqual(1f, parked.Level(LoopSlot.EngineIdle, Tuning, false), 1e-3f);
            Assert.AreEqual(0f, parked.Level(LoopSlot.EngineMove, Tuning, false), 1e-3f);
            Assert.AreEqual(1f, parked.PitchScale(LoopSlot.EngineMove, Tuning), 1e-3f);
            Assert.AreEqual(1f, cruising.Level(LoopSlot.EngineMove, Tuning, false), 0.01f);
            Assert.AreEqual(0f, cruising.Level(LoopSlot.EngineIdle, Tuning, false), 0.01f);
            Assert.AreEqual(1.2f, cruising.PitchScale(LoopSlot.EngineMove, Tuning), 0.01f);
        }

        [Test]
        public void HalfSpeed_PlaysBothEnginesHalfway()
        {
            var state = Ran(3f, 5f);

            Assert.AreEqual(0.5f, state.Level(LoopSlot.EngineMove, Tuning, false), 0.02f);
            Assert.AreEqual(0.5f, state.Level(LoopSlot.EngineIdle, Tuning, false), 0.02f);
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
    }
}
