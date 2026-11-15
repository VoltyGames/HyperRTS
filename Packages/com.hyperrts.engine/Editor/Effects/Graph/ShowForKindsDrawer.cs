using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>Draws a settings group as a foldout of its fields, hidden while its layer's kind is not listed.</summary>
    [CustomPropertyDrawer(typeof(ShowForKindsAttribute))]
    public sealed class ShowForKindsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var group = new Foldout { text = property.displayName, value = true };
            var end = property.GetEndProperty();
            var child = property.Copy();
            for (var more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end); more = child.NextVisible(false))
            {
                group.Add(new PropertyField(child.Copy()));
            }

            var path = property.propertyPath;
            var kind = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')) + ".kind");
            var kinds = ((ShowForKindsAttribute)attribute).Kinds;
            group.SetHidden(!kinds.Contains((EffectLayerKind)kind.enumValueIndex));
            group.TrackPropertyValue(kind, changed => group.SetHidden(!kinds.Contains((EffectLayerKind)changed.enumValueIndex)));
            return group;
        }
    }
}
