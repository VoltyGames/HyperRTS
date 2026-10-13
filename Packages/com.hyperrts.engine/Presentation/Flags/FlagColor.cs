using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>HyperRTS/Flag's hoist band: the shown faction's colour in linear RGB, A = how much shows (0 = none).</summary>
    [MaterialProperty("_FlagColor")]
    public struct FlagColor : IComponentData
    {
        public float4 Value;
    }
}
