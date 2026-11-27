using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Generators;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Generators;

/// <summary>A batch of imported paths marks a generator, or only the items whose inputs it touched, to run.</summary>
public class GeneratorImportTests
{
    private const string Input = "Assets/_GeneratorImportTests/in";

    [TearDown]
    public void ForgetTestGenerator() => GeneratorImport.Changed.Remove(TestGenerator.TestName);

    [Test]
    public void AGeneratorWithoutItemsRunsWholeWhenAnInputChanged()
    {
        GeneratorImport.Collect(new TestGenerator(), new List<string> { Input + "/a.png" });

        Assert.IsTrue(GeneratorImport.Changed.TryGetValue(TestGenerator.TestName, out var names));
        Assert.IsEmpty(names);
    }

    [Test]
    public void PathsOutsideTheInputsMarkNothing()
    {
        GeneratorImport.Collect(new TestGenerator(), new List<string> { Input + "x/a.png", "Assets/other.png" });
        GeneratorImport.Collect(WithItems(), new List<string> { "Assets/other.png" });

        Assert.IsFalse(GeneratorImport.Changed.ContainsKey(TestGenerator.TestName));
    }

    [Test]
    public void OnlyTheItemsWhoseInputsChangedAreMarked_AndBatchesJoin()
    {
        var generator = WithItems();

        GeneratorImport.Collect(generator, new List<string> { "Assets/_GeneratorImportTests/one/m.fbx" });
        GeneratorImport.Collect(generator, new List<string> { "Assets/_GeneratorImportTests/three/m.fbx" });

        CollectionAssert.AreEquivalent(new[] { "one", "three" }, GeneratorImport.Changed[TestGenerator.TestName].ToArray());
    }

    private static TestGenerator WithItems() => new()
    {
        TestItems = new[] { "one", "two", "three" }.Select(name => new GeneratorItem(
            name, new[] { "Assets/_GeneratorImportTests/" + name }, Array.Empty<string>(),
            "Assets/_GeneratorImportTests/" + name, () => { })).ToList(),
    };

    // Not Available, so the Generators tab and GeneratorRunner never list it.
    private sealed class TestGenerator : Generator
    {
        public const string TestName = "Generator import test";

        public IReadOnlyList<GeneratorItem> TestItems = Array.Empty<GeneratorItem>();

        public override string Name => TestName;
        public override string Group => "Tests";
        public override int Order => 0;
        public override string Description => "Reads test paths.";
        public override string Sources => Input;
        public override IEnumerable<string> Inputs => new[] { Input };
        public override string Outputs => "Nothing";
        public override bool Available => false;
        public override bool RunOnImport => true;
        public override IReadOnlyList<GeneratorItem> Items => TestItems;
    }
}
