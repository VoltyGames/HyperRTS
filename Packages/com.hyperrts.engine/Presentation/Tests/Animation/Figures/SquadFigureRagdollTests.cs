using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>
    /// Ragdoll corpses: the handover pose, the slot cap, freeing the slot when the corpse hides and the figures that fall
    /// after their squad's ghost is gone. Physics doesn't step here, so the bodies hold their handover pose.
    /// </summary>
    public class SquadFigureRagdollTests : SquadFigureRagdollFixture
    {
        private const float Tolerance = 5e-3f;

        [SetUp]
        public void AddRagdolls() => UseRagdolls();

        [Test]
        public void Handover_DrawsTheClipPoseFromASlotUntilTheFigureWouldHide()
        {
            Update();
            SetLiving(3);
            Update();
            Advance(RagdollTuning.Default.handoverTime);

            using var corpses = Corpses();
            Assert.AreEqual(1, corpses.Length);
            var corpse = _em.GetComponentData<Corpse>(corpses[0]);
            var copy = _em.GetBuffer<LinkedEntityGroup>(corpses[0])[1].Value;
            Assert.AreEqual(FigureState.Hidden, State(3));
            Assert.IsFalse(Drawn(3));
            Assert.IsTrue(_em.IsComponentEnabled<MaterialMeshInfo>(copy));
            Assert.AreEqual((float)corpse.Slot, _em.GetComponentData<PoseSlot>(copy).Value);

            var handoverAt = corpse.SettleAt - RagdollTuning.Default.settleTime;
            var blend = Lower(3).BlendAt(handoverAt);
            var hips = ClipSkin(0, blend);
            AssertSkin(hips, Slots().Read(corpse.Slot, 0));
            AssertSkin(math.mul(hips, ClipSkin(1, blend)), Slots().Read(corpse.Slot, 1));

            Advance(DieHold);

            using var left = Corpses();
            Assert.AreEqual(0, left.Length);
            Assert.AreEqual(0, Slots().InUse);
        }

        [Test]
        public void FullSlots_KeepTheRestOnTheirBakedFall()
        {
            var slots = Slots();
            for (var i = 0; i < PoseSlots.Most - 1; i++)
            {
                slots.TryTake(out _);
            }

            Update();
            SetLiving(1);
            Update();
            Advance(RagdollTuning.Default.handoverTime);

            using var corpses = Corpses();
            Assert.AreEqual(1, corpses.Length);
            var falling = 0;
            for (var i = 1; i < 4; i++)
            {
                Assert.IsTrue(Figure(i).HandedOver);
                falling += State(i) == FigureState.Die ? 1 : 0;
            }

            Assert.AreEqual(2, falling);
        }

        [TestCase(4, TestName = "A squad wiped in one hit leaves a corpse of every member")]
        [TestCase(1, TestName = "A squad wiped while members fall leaves those corpses and the last")]
        public void SquadGhostGone_ItsShownFiguresFallAsCorpses(int living)
        {
            var prefab = _em.Instantiate(_squad);
            using var parts = _em.GetBuffer<LinkedEntityGroup>(prefab).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            _em.AddComponent<Prefab>(parts);
            _kit.AddRegistry();
            _kit.AddCues();
            Update();
            SetLiving(living);
            Update();

            _em.DestroyEntity(_squad);
            _kit.PushCue(new CueEvent { TypeId = SquadType, Slot = CueSlot.Death, Position = float3.zero });
            Update();

            using var corpses = Corpses();
            Assert.AreEqual(4, corpses.Length);
            foreach (var corpse in corpses)
            {
                var copy = _em.GetBuffer<LinkedEntityGroup>(corpse)[1].Value;
                Assert.IsTrue(_em.IsComponentEnabled<MaterialMeshInfo>(copy));
                Assert.IsTrue(Plays(_em.GetComponentData<LowerClip>(copy).Clip, Range(40, false)));
            }
        }

        private static float4x4 ClipSkin(int bone, in ClipBlend blend)
        {
            var from = RigSkin(bone, blend.Row);
            var to = RigSkin(bone, blend.NextRow);
            return new float4x4(math.lerp(from.c0, to.c0, blend.Blend), math.lerp(from.c1, to.c1, blend.Blend),
                math.lerp(from.c2, to.c2, blend.Blend), math.lerp(from.c3, to.c3, blend.Blend));
        }

        private static void AssertSkin(in float4x4 expected, in float4x4 written)
        {
            for (var c = 0; c < 4; c++)
            {
                for (var r = 0; r < 3; r++)
                {
                    Assert.AreEqual(expected[c][r], written[c][r], Tolerance, $"column {c} row {r}");
                }
            }
        }
    }
}
