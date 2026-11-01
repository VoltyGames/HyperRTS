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

    /// <summary>A component with stat fields and its run of <see cref="StatBindings.Fields"/>.</summary>
    public struct StatBinding
    {
        public ComponentType Component;

        /// <summary>The component's size in floats.</summary>
        public int Floats;

        public int First;
        public int Count;
    }

    /// <summary>
    /// Every field marked with <see cref="StatFieldAttribute"/>, grouped by component, so the stat jobs read and write
    /// them without knowing the component types.
    /// </summary>
    public struct StatBindings : IDisposable
    {
        public NativeArray<StatBinding> Components;
        public NativeArray<StatField> Fields;

        /// <summary>Collects the marked fields; throws when one is not a float or a stat has two fields.</summary>
        public static StatBindings Find(Allocator allocator)
        {
            var components = new List<StatBinding>();
            var fields = new List<StatField>();
            var bound = new HashSet<Stat>();
            foreach (var (component, field, stat) in StatFieldAttribute.Marked)
            {
                Check(component, field, stat, bound);
                var type = TypeManager.GetTypeIndex(component);
                if (components.Count == 0 || components[^1].Component.TypeIndex != type)
                {
                    components.Add(new StatBinding
                    {
                        Component = ComponentType.FromTypeIndex(type),
                        Floats = TypeManager.GetTypeInfo(type).TypeSize / sizeof(float),
                        First = fields.Count,
                    });
                }

                var binding = components[^1];
                binding.Count++;
                components[^1] = binding;
                fields.Add(new StatField { Stat = stat, Index = UnsafeUtility.GetFieldOffset(field) / sizeof(float) });
            }

            return new StatBindings
            {
                Components = new NativeArray<StatBinding>(components.ToArray(), allocator),
                Fields = new NativeArray<StatField>(fields.ToArray(), allocator),
            };
        }

        /// <summary>The stat fields of <paramref name="binding"/>.</summary>
        public readonly NativeArray<StatField> FieldsFor(in StatBinding binding) =>
            Fields.GetSubArray(binding.First, binding.Count);

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
