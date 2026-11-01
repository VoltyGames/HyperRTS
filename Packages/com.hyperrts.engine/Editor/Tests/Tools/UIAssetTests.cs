using HyperRTS.Editor.Validation;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>Every stylesheet link in the engine's UI resolves and every class its UXML uses is styled or named by code.</summary>
    public class UIAssetTests
    {
        private const string Package = "Packages/com.hyperrts.engine";

        [Test]
        public void EngineUIReferencesResolve()
        {
            var issues = new UIAssetCheck(Package).Issues(Package);

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }
    }
}
