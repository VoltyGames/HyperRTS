using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>Saves a template as a prefab once its name is confirmed in the Project window.</summary>
    internal sealed class SaveTemplatePrefab : AssetCreationEndAction
    {
        public EntityTemplate Template { get; set; }

        public override void Action(EntityId entityId, string pathName, string resourceFile)
        {
            var go = Template.Create(Path.GetFileNameWithoutExtension(pathName));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, AssetDatabase.GenerateUniqueAssetPath(pathName));
            Object.DestroyImmediate(go);
            ProjectWindowUtil.ShowCreatedAsset(prefab);
        }
    }
}
