using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Rendering.LitTeam
{
    /// <summary>Detail maps: LitTeam declares them and every material starts without them.</summary>
    public class DetailTests
    {
        [Test]
        public void Detail_IsOffByDefault_AndHasItsProperties()
        {
            var shader = Shader.Find("HyperRTS/LitTeam");
            var material = new Material(shader);
            try
            {
                Assert.IsFalse(material.IsKeywordEnabled("_DETAIL"));
                foreach (var name in new[] { "_DetailAlbedo", "_DetailNormal", "_DetailTiling", "_DetailAlbedoStrength",
                             "_DetailNormalStrength", "_DetailFadeStart", "_DetailFadeEnd" })
                {
                    Assert.IsTrue(material.HasProperty(name), name);
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
