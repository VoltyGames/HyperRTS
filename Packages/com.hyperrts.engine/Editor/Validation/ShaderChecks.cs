using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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

        private static string Messages(Shader shader) => string.Join("; ",
            ShaderUtil.GetShaderMessages(shader).Select(message => $"{message.message} ({message.file}:{message.line})"));
    }
}
