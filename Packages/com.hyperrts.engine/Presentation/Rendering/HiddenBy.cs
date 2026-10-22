using System;
using Unity.Entities;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Why a mesh isn't drawn; a mesh draws only while it has no reason.</summary>
    [Flags]
    public enum HideReason : byte
    {
        Fog = 1 << 0,
        Inside = 1 << 1,

        /// <summary>Not the damage or resource stage on screen.</summary>
        Stage = 1 << 2,

        /// <summary>The intact piece of a broken part, or the broken piece of an intact one.</summary>
        Part = 1 << 3,

        /// <summary>A squad figure out of play.</summary>
        Figure = 1 << 4,
    }

    /// <summary>
    /// Every <see cref="HideReason"/> set on a mesh or gameplay root. A root's own reasons record what its parts
    /// were given, so <see cref="RenderSwitch"/> walks them only on a flip.
    /// </summary>
    public struct HiddenBy : IComponentData
    {
        public HideReason Reasons;

        public readonly bool Has(HideReason reason) => (Reasons & reason) != 0;

        public void Set(HideReason reason, bool hide) => Reasons = hide ? Reasons | reason : Reasons & ~reason;
    }
}
