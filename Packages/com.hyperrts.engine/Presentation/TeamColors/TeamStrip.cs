using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>HyperRTS/LitTeam's team colour band on a render entity: x bottom, y top, in object-space metres.</summary>
    [MaterialProperty("_TeamStrip")]
    public struct TeamStrip : IComponentData
    {
        public float4 Value;
    }
}
