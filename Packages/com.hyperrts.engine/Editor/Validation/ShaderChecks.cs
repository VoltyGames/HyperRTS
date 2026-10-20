using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Finds shaders that fail to compile, for the engine's and a game's shader tests.</summary>
    public static class ShaderChecks
    {
        /// <summary>One "path: messages" line per shader under <paramref name="folder"/> that has errors.</summary>
        public static List<string> Broken(string folder)
        {
            var broken = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (ShaderUtil.ShaderHasError(shader))
                {
                    broken.Add($"{path}: {Messages(shader)}");
                }
            }

            return broken;
        }

        /// <summary>One "pass: messages" line per pass whose vertex stage fails to compile with these keywords.</summary>
        public static List<string> VertexVariantErrors(Shader shader, params string[] keywords)
        {
            var errors = new List<string>();
            var subshader = ShaderUtil.GetShaderData(shader).ActiveSubshader;
            for (var i = 0; i < subshader.PassCount; i++)
            {
                var pass = subshader.GetPass(i);
                var compiled = pass.CompileVariant(ShaderType.Vertex, keywords, ShaderCompilerPlatform.D3D,
                    BuildTarget.StandaloneWindows64);
                if (!compiled.Success)
                {
                    errors.Add($"{pass.Name}: {Lines(compiled.Messages)}");
                }
            }

            return errors;
        }

        private static string Messages(Shader shader) => Lines(ShaderUtil.GetShaderMessages(shader));

        private static string Lines(IEnumerable<ShaderMessage> messages) =>
            string.Join("; ", messages.Select(message => $"{message.message} ({message.file}:{message.line})"));
    }
}
