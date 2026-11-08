using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Destruction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>
    /// Section views follow the building's broken mask: quietly on the first frame, as a clip of pieces for a break
    /// this client saw, and back to standing on repair. Models other than the intact one are copied in while they show.
    /// </summary>
    public class SectionViewTests : SectionViewFixture
    {
        [Test]
        public void TheMaskDrivesTheViews()
        {
            var house = Building(0b0101ul, false, out var models);

            Update(0f);

            AssertBroken(models, true, false, true, false);
            Assert.AreEqual(0b0101ul, _em.GetComponentData<ShownSections>(house).Broken);
        }

        [Test]
        public void Break_AndRepair_FlipOnlyThatSection()
        {
            var house = Building(0ul, false, out var models);
            Update(0f);

            SetBroken(house, 0b1000ul);
            Update(0f);
            AssertBroken(models, false, false, false, true);

            SetBroken(house, 0ul);
            Update(0f);
            AssertBroken(models, false, false, false, false);
        }

        [Test]
        public void SeenBreak_PlaysItsClipOnceThenShowsTheHeap()
        {
            var house = Building(0ul, true, out var models);
            Update(0f);

            SetBroken(house, 0b0100ul);
            Update(10f);

            var piece = models[2];
            var started = _em.GetComponentData<PlayingClip>(Copy(piece.Pieces)).Value;
            Assert.AreEqual(new float4(61f, 60f, -30f, 10f), started, "falls apart alone");
            AssertDrawn(piece, intact: false, pieces: true, stub: true, heap: false);

            Update(10f + Clips.Collapse.Seconds + 0.1f);
            AssertDrawn(piece, intact: false, pieces: false, stub: true, heap: true);
            AssertDrawn(models[0], intact: true, pieces: false, stub: false, heap: false);
        }

        [Test]
        public void UnseenBreak_ShowsTheEndStateWithoutAClip()
        {
            var house = Building(0ul, true, out var models);
            Update(0f);
            var shown = _em.GetComponentData<ShownSections>(house);
            shown.Seen = false;
            _em.SetComponentData(house, shown);

            SetBroken(house, 0b0011ul);
            Update(5f);

            AssertDrawn(models[0], intact: false, pieces: false, stub: true, heap: true);
            AssertDrawn(models[1], intact: false, pieces: false, stub: true, heap: true);
            AssertDrawn(models[2], intact: true, pieces: false, stub: false, heap: false);
            Assert.IsTrue(_em.GetComponentData<ShownSections>(house).Seen);
        }

        [Test]
        public void Stub_HidesWhileNoSupportStands_AndReturnsOnRepair()
        {
            var house = Building(0ul, true, out var models);
            var sections = _em.GetBuffer<Section>(house);
            var resting = sections[2];
            resting.Supports = 0b1000ul;
            sections[2] = resting;
            Update(0f);
            SetBroken(house, 0b0100ul);
            Update(1f);
            Update(1f + Clips.Collapse.Seconds + 0.1f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: true, heap: true);

            SetBroken(house, 0b1100ul);
            Update(10f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: false, heap: true);

            SetBroken(house, 0b0100ul);
            Update(11f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: true, heap: true);
        }

        [Test]
        public void Copies_JoinTheBuildingsGroupWithItsHideReasons_AndLeaveWithRepair()
        {
            var house = Building(0ul, false, out var models);
            Update(0f);
            var standing = _em.GetBuffer<LinkedEntityGroup>(house).Length;
            _em.SetComponentData(house, new HiddenBy { Reasons = HideReason.Inside });

            SetBroken(house, 0b0001ul);
            Update(1f);
            var copy = Copy(models[0].Broken);
            var group = _em.GetBuffer<LinkedEntityGroup>(house).Reinterpret<Entity>().AsNativeArray();
            Assert.IsTrue(group.Contains(copy), "destroyed with the building");
            Assert.AreEqual(house, _em.GetComponentData<Parent>(copy).Value);
            Assert.IsFalse(Drawn(copy), "hidden like the rest of the building");

            SetBroken(house, 0ul);
            Update(2f);
            Assert.IsFalse(_em.Exists(copy));
            Assert.AreEqual(standing, _em.GetBuffer<LinkedEntityGroup>(house).Length);
        }
    }
}
