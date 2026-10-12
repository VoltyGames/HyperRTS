using System.Linq;
using HyperRTS.Editor.Generators;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The generator list behind Tools ▸ Generate: unique names, run order.</summary>
    public class GeneratorTests
    {
        [Test]
        public void NamesAreUnique()
        {
            var names = GeneratorRunner.All().Select(generator => generator.Name).ToList();

            Assert.That(names, Is.Unique);
        }

        [Test]
        public void AllIsInRunOrder()
        {
            var orders = GeneratorRunner.All().Select(generator => generator.Order).ToList();

            Assert.That(orders, Is.Ordered);
        }

        [Test]
        public void EveryGeneratorHasGroupAndDescription()
        {
            foreach (var generator in GeneratorRunner.All())
            {
                Assert.That(generator.Group, Is.Not.Empty, generator.Name);
                Assert.That(generator.Description, Is.Not.Empty, generator.Name);
            }
        }
    }
}
