using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// A type's <typeparamref name="TAttribute"/>s, inherited ones included. Rules ask on every component each
    /// inspector layout, so each type is reflected once.
    /// </summary>
    internal static partial class AttributeCache<TAttribute> where TAttribute : Attribute
    {
        [AutoStaticsCleanup]
        private static readonly Dictionary<Type, TAttribute[]> Cache = new();

        public static TAttribute[] Get(Type type)
        {
            if (!Cache.TryGetValue(type, out var attributes))
            {
                Cache[type] = attributes = type.GetCustomAttributes<TAttribute>(true).ToArray();
            }

            return attributes;
        }
    }
}
