using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>
    /// Marks a float field of an unmanaged component as the live value of a stat: StatBaseSystem records it as the base
    /// when the entity spawns and StatSystem rewrites it from the <see cref="StatModifier"/>s. Games add stats this way.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class StatFieldAttribute : Attribute
    {
        public StatFieldAttribute(Stat stat)
        {
            Stat = stat;
        }

        public Stat Stat { get; }

        /// <summary>Every marked field, by component; component types are fixed once loaded, so they are scanned once.</summary>
        [NoAutoStaticsCleanup]
        internal static readonly IReadOnlyList<(Type Component, FieldInfo Field, Stat Stat)> Marked = Scan();

        private static List<(Type, FieldInfo, Stat)> Scan()
        {
            var marked = new List<(Type, FieldInfo, Stat)>();
            foreach (var info in TypeManager.AllTypes)
            {
                if (!IsUnmanagedComponent(info))
                {
                    continue;
                }

                foreach (var field in info.Type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic))
                {
                    var attribute = field.GetCustomAttribute<StatFieldAttribute>();
                    if (attribute != null)
                    {
                        marked.Add((info.Type, field, attribute.Stat));
                    }
                }
            }

            return marked;
        }

        private static bool IsUnmanagedComponent(in TypeManager.TypeInfo info)
        {
            if (info.Category != TypeManager.TypeCategory.ComponentData)
            {
                return false;
            }

            return info.Type != null && info.Type.IsValueType;
        }
    }
}
