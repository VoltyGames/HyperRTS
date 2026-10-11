using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>How the ground is drawn: the terrain's material and the ground colour foliage blends into.</summary>
    public readonly struct MapGroundLook
    {
        public MapGroundLook(Material material, Texture2D picture)
        {
            Material = material;
            Picture = picture;
        }

        public Material Material { get; }

        public Texture2D Picture { get; }
    }
}
