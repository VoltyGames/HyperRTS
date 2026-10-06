using System.Collections.Generic;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Target for setup helpers, so bakers, tests and runtime code build identical entities.</summary>
    public interface IComponentSink
    {
        void Add<T>() where T : unmanaged, IComponentData;
        void Add<T>(in T component) where T : unmanaged, IComponentData;
        DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData;
        void SetEnabled<T>(bool enabled) where T : unmanaged, IComponentData, IEnableableComponent;
    }

    public static class ComponentSinkExtensions
    {
        /// <summary>Adds a buffer holding <paramref name="items"/>; a null list adds it empty.</summary>
        public static void AddBuffer<TSink, T>(ref this TSink sink, IReadOnlyList<T> items)
            where TSink : struct, IComponentSink where T : unmanaged, IBufferElementData
        {
            var buffer = sink.AddBuffer<T>();
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
