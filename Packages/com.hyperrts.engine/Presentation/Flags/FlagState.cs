using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>HyperRTS/Flag's state on a flag entity: x flag layer, y hoist (0..1), z shown (0/1), w drop in flag heights.</summary>
    [MaterialProperty("_FlagState")]
    public struct FlagState : IComponentData
    {
        public float4 Value;
    }
}
