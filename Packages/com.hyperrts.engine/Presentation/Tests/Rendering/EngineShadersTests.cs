using System.Linq;
using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Rendering
{
    /// <summary>URP's graphics settings find every engine shader by its name.</summary>
    public class EngineShadersTests
    {
        [Test]
        public void EveryShader_IsFound()
        {
            var shaders = EngineShaders.Get();
            var missing = typeof(EngineShaders).GetProperties()
                .Where(property => property.PropertyType == typeof(Shader) && property.GetValue(shaders) == null)
                .Select(property => property.Name);
            Assert.That(missing, Is.Empty, "Engine shaders not found by name.");
        }
    }
}
