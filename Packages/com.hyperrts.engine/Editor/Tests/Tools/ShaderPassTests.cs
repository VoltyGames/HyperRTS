using HyperRTS.Editor.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>
    /// Every hand-written engine shader lit by URP has a Meta pass, so Surface Cache GI bounces its own colour rather
    /// than URP's fallback material's. Opaque and cutout shaders also take part in the shadow, depth and motion vector
    /// prepasses, and every shader that declares DOTS instancing compiles with it.
    /// </summary>
    public class ShaderPassTests
    {
        private const string Package = "Packages/com.hyperrts.engine";

        // Unlit, or drawn indirectly where GI never renders them.
        [NoAutoStaticsCleanup]
        private static readonly HashSet<string> WithoutMeta = new()
        {
            "Hidden/HyperRTS/FoliageGrass",
            "HyperRTS/RowCrop",
            "HyperRTS/Ground Marker",
            "HyperRTS/Health Bar",
            "HyperRTS/Overlay",
        };

        private static readonly ShaderTagId LightMode = new("LightMode");
        private static readonly ShaderTagId RenderType = new("RenderType");
        private static readonly ShaderTagId AlwaysRenderMotionVectors = new("AlwaysRenderMotionVectors");

        private static IEnumerable<string> HandWrittenShaders() =>
            AssetDatabase.FindAssets("t:Shader", new[] { Package })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".shader", StringComparison.Ordinal));

        [TestCaseSource(nameof(HandWrittenShaders))]
        public void ShaderHasItsPrepassesAndCompilesForDOTS(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

            // Opaque and cutout shaders are the ones that cast shadows and fill the depth and normals textures.
            var renderType = shader.FindSubshaderTagValue(0, RenderType).name;
            if (renderType is "Opaque" or "TransparentCutout")
            {
                foreach (var lightMode in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" })
                {
                    Assert.IsTrue(HasPass(shader, lightMode), $"{shader.name} is {renderType} and needs a {lightMode} pass.");
                }
            }

            if (shader.FindSubshaderTagValue(0, AlwaysRenderMotionVectors).name == "true")
            {
                Assert.IsTrue(HasPass(shader, "MotionVectors"), $"{shader.name} sets AlwaysRenderMotionVectors and needs a MotionVectors pass.");
            }

            // Read from the source: the keyword space also lists Unity's global keyword for terrain, whose borrowed
            // picking pass can't compile with it.
            if (DeclaresDOTSInstancing(path))
            {
                var errors = ShaderChecks.VertexVariantErrors(shader, "DOTS_INSTANCING_ON");
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
        }

        private static bool DeclaresDOTSInstancing(string path)
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(path));
            return source.Contains("DOTS_INSTANCING_ON") || source.Contains("InstancingKeywords.hlsl");
        }

        [Test]
        public void EveryLitShaderHasAMetaPass()
        {
            // Hand-written shaders only: Shader Graphs and VFX graphs generate their own passes.
            var shaders = AssetDatabase.FindAssets("t:Shader", new[] { Package })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".shader", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<Shader>)
                .Where(shader => shader != null)
                .ToList();
            Assert.That(shaders, Is.Not.Empty);

            var missing = shaders
                .Where(shader => !WithoutMeta.Contains(shader.name))
                .Where(shader => HasPass(shader, "UniversalForward", "UniversalForwardOnly") && !HasPass(shader, "Meta"))
                .Select(shader => shader.name);
            Assert.That(missing, Is.Empty, "Lit shaders without a Meta pass; add one through Rendering/MetaPass.hlsl.");

            var stale = WithoutMeta.Where(name => shaders.All(shader => shader.name != name));
            Assert.That(stale, Is.Empty, "Exempt shaders that no longer exist.");
        }

        // The passes of the subshader URP runs, UsePass ones included. Unity reports built-in light modes upper case (META).
        private static bool HasPass(Shader shader, params string[] lightModes)
        {
            for (var pass = 0; pass < shader.passCount; pass++)
            {
                if (lightModes.Contains(shader.FindPassTagValue(pass, LightMode).name, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
