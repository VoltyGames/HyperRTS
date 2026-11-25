using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Generators;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Generators
{
    /// <summary>Out of date per generator item, and per whole generator for one without items.</summary>
    public class GeneratorItemTests
    {
        private const string Folder = "Temp/GeneratorItemTests";
        private const string First = Folder + "/first.txt";
        private const string Second = Folder + "/second.txt";

        private DateTime _ran;

        [SetUp]
        public void WriteInputs()
        {
            Directory.CreateDirectory(Folder);
            _ran = DateTime.Now;
            foreach (var file in new[] { First, Second })
            {
                File.WriteAllText(file, "x");
                File.SetLastWriteTime(file, _ran.AddMinutes(-5));
            }
        }

        [TearDown]
        public void DeleteInputs() => Directory.Delete(Folder, true);

        [Test]
        public void TouchingOneItemsInputMakesOnlyThatItemOutOfDate()
        {
            var generator = TwoItems();
            var log = new GeneratorRunLog();
            log.Add(Run("first", null));
            log.Add(Run("second", null));
            Assert.That(generator.IsOutOfDate(generator.Items, log), Is.False);

            File.SetLastWriteTime(First, _ran.AddMinutes(5));

            Assert.That(generator.OutOfDateItems(generator.Items, log).Select(item => item.Name), Is.EqualTo(new[] { "first" }));
            Assert.That(generator.IsOutOfDate(generator.Items, log), Is.True);
        }

        [Test]
        public void FailedOrNeverRunItemIsOutOfDate()
        {
            var generator = TwoItems();
            var log = new GeneratorRunLog();
            log.Add(Run("first", "broken"));

            Assert.That(generator.OutOfDateItems(generator.Items, log).Select(item => item.Name), Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void GeneratorWithoutItemsIsOutOfDateWhenAnInputIsNewerThanItsRun()
        {
            var generator = new TestGenerator();
            var log = new GeneratorRunLog();
            log.Add(Run("", null));
            Assert.That(generator.IsOutOfDate(generator.Items, log), Is.False);

            File.SetLastWriteTime(Second, _ran.AddMinutes(5));

            Assert.That(generator.IsOutOfDate(generator.Items, log), Is.True);
        }

        private static TestGenerator TwoItems() => new()
        {
            TestItems = new[]
            {
                new GeneratorItem("first", new[] { First }, Array.Empty<string>(), First, () => { }),
                new GeneratorItem("second", new[] { Second }, Array.Empty<string>(), Second, () => { }),
            },
        };

        private GeneratorRun Run(string item, string error) => new()
        {
            generator = TestGenerator.TestName,
            item = item,
            time = _ran.ToString(GeneratorRun.TimeFormat, CultureInfo.InvariantCulture),
            error = error,
        };

        // Not Available, so the Generators tab and GeneratorRunner never list it.
        private sealed class TestGenerator : Generator
        {
            public const string TestName = "Generator item test";

            public IReadOnlyList<GeneratorItem> TestItems = Array.Empty<GeneratorItem>();

            public override string Name => TestName;
            public override string Group => "Tests";
            public override int Order => 0;
            public override string Description => "Reads temp files.";
            public override string Sources => Folder;
            public override IEnumerable<string> Inputs => new[] { Folder };
            public override string Outputs => "Nothing";
            public override bool Available => false;
            public override IReadOnlyList<GeneratorItem> Items => TestItems;
        }
    }
}
