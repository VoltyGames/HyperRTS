using HyperRTS.Editor.Effects;
using HyperRTS.Presentation.Effects.Rig;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>The test effects' graphs are current with their recipes, and their shaders compile.</summary>
    public class EffectGraphBuilderTests
    {
        private const string TestBurst = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst" + EffectRecipe.Extension;
        private const string TestStream = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestStream" + EffectRecipe.Extension;

        [TestCase(TestBurst)]
        [TestCase(TestStream)]
        public void TestGraphs_AreCurrentAndCompile(string recipe)
        {
            Assert.IsNull(EffectGraphBuilder.Problem(recipe));
            CollectionAssert.IsEmpty(EffectGraphBuilder.ShaderErrors(EffectRecipe.GraphPath(recipe)));
        }
    }
}
