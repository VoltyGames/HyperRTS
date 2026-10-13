using HyperRTS.Editor.Maps;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    public class MapRandomTests
    {
        [Test]
        public void Count_AveragesAFractionalExpectation()
        {
            var random = new System.Random(7);
            var total = 0;
            for (var i = 0; i < 10000; i++)
            {
                total += random.Count(0.15f);
            }

            Assert.That(total / 10000f, Is.EqualTo(0.15f).Within(0.02f), "small regions still get their share");
        }

        [Test]
        public void Count_StaysWithinTheWholeNumbersAroundTheExpectation()
        {
            var random = new System.Random(3);
            for (var i = 0; i < 1000; i++)
            {
                Assert.That(random.Count(2.4f), Is.InRange(2, 3));
            }

            Assert.AreEqual(5, random.Count(5f));
        }
    }
}
