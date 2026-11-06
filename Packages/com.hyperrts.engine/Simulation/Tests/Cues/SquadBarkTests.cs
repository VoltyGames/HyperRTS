using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Squad barks: which state changes speak, once, only for the local player and not on spawn.</summary>
    public class SquadBarkTests : SimulationFixture
    {
        private static readonly CueSlot[] Slots =
        {
            CueSlot.TakeCover, CueSlot.Suppressed, CueSlot.Pinned, CueSlot.Retreat, CueSlot.DigIn, CueSlot.Reinforced,
        };

        [SetUp]
        public void Prepare()
        {
            // The member system would put cover and actions back to what the ground says.
            ref var members = ref World.World.Unmanaged.GetExistingSystemState<SquadMemberSystem>();
            members.Enabled = false;
        }

        private Entity Squad(byte faction = 1, int size = 4, bool settle = true)
        {
            var squad = World.SpawnSquad(faction, float3.zero, size, speed: 0f);
            var sounds = EntitySoundsAuthoring.Add(World.Writer(squad));
            var cue = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            foreach (var slot in Slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, cue);
            }

            if (settle)
            {
                World.Tick();
            }

            return squad;
        }

        private void Change(Entity squad, int member, CoverKind cover = CoverKind.None, MemberAction action = MemberAction.None,
            float health = SquadTestKit.MemberHealth)
        {
            var members = World.Buffer<SquadMember>(squad);
            var each = members[member];
            each.Cover = cover;
            each.Action = action;
            each.Health = health;
            members[member] = each;
        }

        private void Suppress(Entity squad, float value, bool retreating = false)
        {
            World.Set(squad, new Suppression { Value = value, Quiet = 0f });
            World.SetEnabled<Retreating>(squad, retreating);
        }

        // Holds suppression steady so waiting out the squad interval changes nothing.
        private void Wait(Entity squad, float value, bool retreating = false)
        {
            for (var t = 0f; t < World.Singleton<VoiceRules>().SquadBarkInterval; t += TestWorld.FrameTime)
            {
                Suppress(squad, value, retreating);
                World.Tick();
            }
        }

        private CueSlot[] Barks(float seconds = 0.1f) =>
            World.QueuedOver(seconds).ConvertAll(cue => cue.Slot).FindAll(slot => System.Array.IndexOf(Slots, slot) >= 0).ToArray();

        [Test]
        public void EachChange_PlaysItsCueOnce()
        {
            var squad = Squad();
            Suppress(squad, 0.5f);
            CollectionAssert.AreEqual(new[] { CueSlot.Suppressed }, Barks(), "Suppressed");

            Wait(squad, 0.5f);
            Suppress(squad, 0.95f);
            CollectionAssert.AreEqual(new[] { CueSlot.Pinned }, Barks(), "Pinned, not suppressed too");

            Wait(squad, 0.95f);
            Suppress(squad, 0.95f, retreating: true);
            CollectionAssert.AreEqual(new[] { CueSlot.Retreat }, Barks(), "Retreat");

            Wait(squad, 0.95f, retreating: true);
            Change(squad, 0, health: 0f);
            Barks();
            Wait(squad, 0.95f, retreating: true);
            Change(squad, 0);
            CollectionAssert.AreEqual(new[] { CueSlot.Reinforced }, Barks(), "Reinforced");

            Wait(squad, 0.95f, retreating: true);
            Change(squad, 1, action: MemberAction.Dig);
            CollectionAssert.AreEqual(new[] { CueSlot.DigIn }, Barks(), "DigIn");

            Wait(squad, 0.95f, retreating: true);
            for (var i = 0; i < 3; i++)
            {
                Change(squad, i, CoverKind.Heavy);
            }

            CollectionAssert.AreEqual(new[] { CueSlot.TakeCover }, Barks(), "TakeCover");
            CollectionAssert.IsEmpty(Barks(1f), "a state that holds does not bark again");
        }

        [Test]
        public void ARiseInsideTheSquadInterval_StaysQuiet()
        {
            var squad = Squad();
            Suppress(squad, 0.5f);
            Assert.AreEqual(1, Barks().Length);

            Suppress(squad, 0.95f);
            CollectionAssert.IsEmpty(Barks(World.Singleton<VoiceRules>().SquadBarkInterval - 1f), "the squad spoke a moment ago");
        }

        [Test]
        public void TwoSquads_SpeakOncePerPlayerInterval()
        {
            var first = Squad();
            var second = Squad();
            Suppress(first, 0.5f);
            Suppress(second, 0.5f);

            Assert.AreEqual(1, Barks(World.Singleton<VoiceRules>().SquadBarkPlayerInterval - 0.2f).Length);
        }

        [Test]
        public void Spawn_DoesNotBark()
        {
            var squad = Squad(settle: false);
            Suppress(squad, 0.95f);
            Change(squad, 0, CoverKind.Heavy, MemberAction.Dig);

            CollectionAssert.IsEmpty(Barks());
            Assert.IsTrue(World.Get<SquadBarkState>(squad).Known, "the first look only records");
        }

        [Test]
        public void AnotherPlayersSquad_DoesNotBark()
        {
            var enemy = Squad(faction: 2);
            Suppress(enemy, 0.95f);

            CollectionAssert.IsEmpty(Barks());
        }
    }
}
