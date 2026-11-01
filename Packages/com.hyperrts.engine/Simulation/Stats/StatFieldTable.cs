using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A stat a component field holds, by the field's place in the component read as floats.</summary>
    public struct StatField
    {
        public Stat Stat;
        public int Index;
    }

    /// <summary>
    /// Every field marked with <see cref="StatFieldAttribute"/>, grouped by component, so the stat jobs read and write
    /// them without knowing the component types.
    /// </summary>
    public struct StatFieldTable : IDisposable
    {
        public NativeArray<StatComponent> Components;
        public NativeArray<StatField> Fields;

        /// <summary>Collects the marked fields; throws when one is not a float or a stat has two fields.</summary>
        public static StatFieldTable Find(Allocator allocator)
        {
            var components = new List<StatComponent>();
            var fields = new List<StatField>();
            var bound = new HashSet<Stat>();
            foreach (var (component, field, stat) in StatFieldAttribute.Marked)
            {
                Check(component, field, stat, bound);
                var type = TypeManager.GetTypeIndex(component);
                if (components.Count == 0 || components[^1].Component.TypeIndex != type)
                {
                    components.Add(new StatComponent
                    {
                        Component = ComponentType.FromTypeIndex(type),
                        Floats = TypeManager.GetTypeInfo(type).TypeSize / sizeof(float),
                        First = fields.Count,
                    });
                }

                var statComponent = components[^1];
                statComponent.Count++;
                components[^1] = statComponent;
                fields.Add(new StatField { Stat = stat, Index = UnsafeUtility.GetFieldOffset(field) / sizeof(float) });
            }

            return new StatFieldTable
            {
                Components = new NativeArray<StatComponent>(components.ToArray(), allocator),
                Fields = new NativeArray<StatField>(fields.ToArray(), allocator),
            };
        }

        /// <summary>The stat fields of <paramref name="statComponent"/>.</summary>
        public readonly NativeArray<StatField> FieldsFor(in StatComponent statComponent) =>
            Fields.GetSubArray(statComponent.First, statComponent.Count);

        public void Dispose()
        {
            Components.Dispose();
            Fields.Dispose();
        }

        private static void Check(Type component, FieldInfo field, Stat stat, HashSet<Stat> bound)
        {
            if (field.FieldType != typeof(float))
            {
                throw new InvalidOperationException($"{component.Name}.{field.Name} holds {stat} but is not a float.");
            }

            if (!bound.Add(stat))
            {
                throw new InvalidOperationException($"{component.Name}.{field.Name} holds {stat}, which another field holds.");
            }
        }
    }
}
