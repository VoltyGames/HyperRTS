using Unity.Mathematics;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>A faction and the linear RGBA colour this client shows it in.</summary>
    public struct FactionColor
    {
        public byte Faction;
        public float4 Color;
    }
}
