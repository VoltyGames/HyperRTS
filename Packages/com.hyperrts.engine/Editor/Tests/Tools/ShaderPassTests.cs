using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>
    /// Every hand-written engine shader lit by URP has a Meta pass, so Surface Cache GI bounces its own colour rather
    /// than URP's fallback material's.
    /// </summary>
    public class ShaderPassTests
    {
        private const string Package = "Packages/com.hyperrts.engine";

        // Unlit, or drawn indirectly where GI never renders them.
        private static readonly HashSet<string> WithoutMeta = new()
        {
            "Hidden/HyperRTS/FoliageGrass",
            "HyperRTS/RowCrop",
            "HyperRTS/Ground Marker",
            "HyperRTS/Health Bar",
            "HyperRTS/Overlay",
        };

        private static readonly ShaderTagId LightMode = new("LightMode");

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
