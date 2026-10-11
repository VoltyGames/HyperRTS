using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// A completed building or finished research of this <see cref="EntityInfo.TypeId"/> the owner must have before
    /// producing.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct Prerequisite : IBufferElementData
    {
        public int TypeId;
    }
}
