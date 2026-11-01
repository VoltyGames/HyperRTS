using Unity.Entities;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>Written by the HUD, read by input: clicks over UI must not reach the world.</summary>
    public struct PointerState : IComponentData
    {
        public bool OverUI;
    }
}
