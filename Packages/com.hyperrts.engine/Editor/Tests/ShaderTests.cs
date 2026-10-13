using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class ShaderTests
    {
        [Test]
        public void EngineShadersCompileWithoutErrors()
        {
            var broken = AssetDatabase.FindAssets("t:Shader", new[] { "Packages/com.hyperrts.engine" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path)))
                .Select(path => $"{path}: {Messages(AssetDatabase.LoadAssetAtPath<Shader>(path))}");

            Assert.IsEmpty(broken);
        }

        private static string Messages(Shader shader) => string.Join("; ",
            ShaderUtil.GetShaderMessages(shader).Select(message => $"{message.message} ({message.file}:{message.line})"));
    }
}
