using System;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>Settings of a <see cref="TerrainLayerFactory"/> item.</summary>
    [Serializable]
    public sealed class TerrainLayerParams
    {
        /// <summary>The texture set beside the item's file: <c>T_&lt;textureSet&gt;_BaseColor/Normal/MaskMap.png</c>.</summary>
        public string textureSet;

        /// <summary>Largest import size of the set's textures (px).</summary>
        public int textureSize;

        /// <summary>Metres one texture tile covers, from the real size of what the photo shows.</summary>
        public float tileSize;
    }
}
