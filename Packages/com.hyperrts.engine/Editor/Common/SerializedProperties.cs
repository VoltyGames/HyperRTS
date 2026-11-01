using System.Collections.Generic;
using UnityEditor;

namespace HyperRTS.Editor.Common
{
    /// <summary>Walks serialized properties for drawers and panels that lay out a struct's fields themselves.</summary>
    public static class SerializedProperties
    {
        /// <summary>A copy of each visible field directly inside <paramref name="property"/>, in order.</summary>
        public static IEnumerable<SerializedProperty> VisibleChildren(this SerializedProperty property)
        {
            var end = property.GetEndProperty();
            var child = property.Copy();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                yield return child.Copy();
            }
        }
    }
}
