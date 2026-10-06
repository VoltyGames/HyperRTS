using System.Collections.Generic;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Target for setup helpers, so bakers, tests and runtime code build identical entities.</summary>
    public interface IEntityWriter
    {
        void Add<T>() where T : unmanaged, IComponentData;
        void Add<T>(in T component) where T : unmanaged, IComponentData;
        DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData;
        void SetEnabled<T>(bool enabled) where T : unmanaged, IComponentData, IEnableableComponent;
    }

    public static class EntityWriterExtensions
    {
        /// <summary>Adds a buffer holding <paramref name="items"/>; a null list adds it empty.</summary>
        public static void AddBuffer<TWriter, T>(ref this TWriter writer, IReadOnlyList<T> items)
            where TWriter : struct, IEntityWriter where T : unmanaged, IBufferElementData
        {
            var buffer = writer.AddBuffer<T>();
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                buffer.Add(item);
            }
        }
    }
}
