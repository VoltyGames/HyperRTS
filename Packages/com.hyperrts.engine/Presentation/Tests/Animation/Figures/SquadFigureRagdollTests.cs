using HyperRTS.Presentation.Animation.Bones;
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
    /// Ragdoll corpses for the figures that fall after their squad's ghost is gone. Physics doesn't step here, so the
    /// bodies hold their handover pose.
    /// </summary>
    public class SquadFigureRagdollTests : SquadFigureRagdollFixture
    {
        [SetUp]
        public void AddRagdolls() => UseRagdolls();

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
    }
}
