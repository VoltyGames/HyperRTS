using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A terrain detail prototype: an instanced mesh prefab, or a grass texture drawn as billboards.</summary>
    [Serializable]
    public sealed class MapClimateDetail : IMapKeyed
    {
        public string key;

        [Tooltip("Instanced mesh; leave empty to use the texture.")]
        public GameObject mesh;

        public Texture2D texture;
        public Vector2 width = new(0.6f, 1.2f);
        public Vector2 height = new(0.4f, 0.9f);
        public Color healthy = Color.white;
        public Color dry = new(0.85f, 0.8f, 0.6f);

        [Tooltip("Instances per square metre at full coverage.")]
        public float density = 2f;

        string IMapKeyed.Key => key;
    }
}
