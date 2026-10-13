using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>HyperRTS/LitTeam's team colour on a render entity: linear RGB, A = how much shows (0 = none).</summary>
    [MaterialProperty("_TeamColor")]
    public struct TeamColor : IComponentData
    {
        public float4 Value;
    }
}
