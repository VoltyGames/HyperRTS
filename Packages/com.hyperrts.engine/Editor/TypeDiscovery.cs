using System;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor
{
    /// <summary>One instance of every concrete <typeparamref name="T"/>, so games extend editor tools by subclassing.</summary>
    internal static class TypeDiscovery
    {
        public static T[] Instances<T>() where T : class =>
            TypeCache.GetTypesDerivedFrom<T>()
                .Where(type => !type.IsAbstract && !type.ContainsGenericParameters)
                .OrderBy(type => type.FullName)
                .Select(type => (T)Activator.CreateInstance(type))
                .ToArray();
    }
}
