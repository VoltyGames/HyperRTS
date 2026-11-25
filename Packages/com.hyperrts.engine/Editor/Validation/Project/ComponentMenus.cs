using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Project
{
    /// <summary>Finds components without an Add Component menu, so each is either listed under a path or hidden on purpose.</summary>
    public static class ComponentMenus
    {
        /// <summary>The concrete components in non-test assemblies named <paramref name="prefix"/>... that declare no menu, sorted.</summary>
        public static List<string> Missing(string prefix) =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && Owned(type.Assembly, prefix))
                .Where(type => type.GetCustomAttribute<AddComponentMenu>(false) == null)
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToList();

        private static bool Owned(Assembly assembly, string prefix)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith(prefix) && !name.EndsWith(".Tests");
        }
    }
}
