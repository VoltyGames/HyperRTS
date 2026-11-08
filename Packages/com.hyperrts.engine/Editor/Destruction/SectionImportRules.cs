using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>Keeps a sectioned building's sections, read from its FBX properties, inside the imported model.</summary>
    public sealed class SectionImportRules : AssetPostprocessor
    {
        private ModelSections _sections;

        private void OnPostprocessGameObjectWithUserProperties(GameObject node, string[] names, object[] values)
        {
            var properties = new FbxProperties(node.name, names, values);
            if (_sections == null && !properties.Has(ModelSections.SectionProperty) && !properties.Has(ModelSections.HoleProperty))
            {
                return;
            }

            if (_sections == null)
            {
                _sections = ScriptableObject.CreateInstance<ModelSections>();
                _sections.name = "Sections";
            }

            _sections.Read(node, properties);
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (_sections == null)
            {
                return;
            }

            _sections.Check(assetPath);
            context.AddObjectToAsset("sections", _sections);
        }
    }
}
