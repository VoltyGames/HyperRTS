using System;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>Settings of a <see cref="PhotoMaterialFactory"/> item.</summary>
    [Serializable]
    public sealed class PhotoMaterialParams
    {
        /// <summary>The texture set beside the item's file: <c>T_&lt;textureSet&gt;_BaseColor/Normal/MaskMap.png</c>.</summary>
        public string textureSet;

        /// <summary>Largest import size of the set's textures (px).</summary>
        public int textureSize;

        /// <summary>Scales the mask map's smoothness (alpha).</summary>
        public float smoothness;
    }
}
