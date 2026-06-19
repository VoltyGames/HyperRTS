using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Core.Selection
{
    /// <summary>Linear-RGBA colours <see cref="SelectionHighlightSystem"/> writes to the base-colour override.</summary>
    public struct SelectionHighlightColors : IComponentData
    {
        public float4 Selected;
        public float4 Deselected;
    }
}
