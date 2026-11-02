using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>Which fields Unity and JsonUtility serialize, and the item type of a serialized list or array.</summary>
    internal static class UnitySerialization
    {
        public static bool IsSerialized(FieldInfo field)
        {
            if (field.IsInitOnly || field.IsNotSerialized)
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField));
        }

        /// <summary>The item type of a <see cref="List{T}"/> or array, or null for any other type.</summary>
        public static Type ItemType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;
        }
    }
}
