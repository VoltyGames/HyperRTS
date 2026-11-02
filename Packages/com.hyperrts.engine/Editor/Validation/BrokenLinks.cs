using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>An asset's links to deleted assets: they keep their id but load as null.</summary>
    public static class BrokenLinks
    {
        /// <summary>The property path of every broken link in <paramref name="asset"/>.</summary>
        public static IEnumerable<string> Find(Object asset)
        {
            var property = new SerializedObject(asset).GetIterator();
            while (property.Next(true))
            {
                var broken = property.propertyType == SerializedPropertyType.ObjectReference
                             && property.objectReferenceValue == null
                             && property.objectReferenceEntityIdValue != EntityId.None;
                if (broken)
                {
                    yield return property.propertyPath;
                }
            }
        }
    }
}
