using HyperRTS.Editor.Validation;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    public class ShaderTests
    {
        [Test]
        public void EngineShadersCompileWithoutErrors() => Assert.IsEmpty(ShaderChecks.Broken("Packages/com.hyperrts.engine"));
    }
}
