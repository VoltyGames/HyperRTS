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
}
