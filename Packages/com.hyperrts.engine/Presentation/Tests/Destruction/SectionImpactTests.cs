using HyperRTS.Presentation.Animation.Clips;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>
    /// Impacts this client saw hole the section nearest them and pick which way it blows when it breaks.
    /// </summary>
    public class SectionImpactTests : SectionViewFixture
    {
        [Test]
        public void SeenImpacts_PickTheNearestHole_AndUnseenOnesAreIgnored()
        {
            Building(0ul, true, out var models);
            AddFog();
            Update(0f);

            // Section 1 is west of the building at x = 6 (seen); section 0 is east at x = 14 (in fog).
            Impact(new float3(5.2f, 1f, 6f));
            Impact(new float3(15.2f, 1f, 6f));
            Update(1f);

            AssertHole(models[1], 1);
            AssertDrawn(models[0], intact: true, pieces: false, stub: false, heap: false);
        }

        [TestCase(5.2f, 1f, TestName = "From outside it blows in")]
        [TestCase(6.8f, 31f, TestName = "From inside it blows out")]
        public void Break_AfterASeenImpact_PicksTheClipByTheSide(float x, float startRow)
        {
            var house = Building(0ul, true, out var models);
            AddFog();
            Update(0f);
            Impact(new float3(x, 1f, 6f));
            Update(1f);

            SetBroken(house, 0b0010ul);
            Update(1.5f);

            Assert.AreEqual(startRow, _em.GetComponentData<PlayingClip>(Copy(models[1].Pieces)).Value.x);
        }
    }
}
