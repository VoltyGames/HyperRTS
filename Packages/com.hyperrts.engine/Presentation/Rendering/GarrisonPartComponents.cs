using Unity.Entities;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>A model part drawn only while its container carries one of its <see cref="GarrisonPartPassenger"/> types.</summary>
    public struct GarrisonPart : IComponentData
    {
        public Entity Container;
    }

    /// <summary>A passenger type, by <c>EntityInfo.TypeId</c>, whose presence aboard shows the part.</summary>
    [InternalBufferCapacity(2)]
    public struct GarrisonPartPassenger : IBufferElementData
    {
        public int TypeId;
    }
}
