using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>Settings of a <see cref="ColorMaterialFactory"/> item.</summary>
    [Serializable]
    public sealed class ColorMaterialParams
    {
        public Color color;
        public float smoothness;
    }
}
