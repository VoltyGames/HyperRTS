using HyperRTS.Simulation.Common.Players;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>An owner and the linear RGBA colour this client shows it in.</summary>
    public struct OwnerColor
    {
        public byte Owner;
        public float4 Color;
    }
}
