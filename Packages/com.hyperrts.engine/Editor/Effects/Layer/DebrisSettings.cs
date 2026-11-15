using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Layer
{
    /// <summary>The chunks a Debris layer draws and how its lit material looks.</summary>
    [Serializable]
    public sealed class DebrisSettings
    {
        [Tooltip("The chunks a debris layer draws, 1 to 4 meshes.")]
        public List<Mesh> meshes = new();

        public float metallic;
        public float smoothness;
    }
}
