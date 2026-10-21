using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Pieces a broken prop throws on the client, such as planks, hay clumps, sandbags or rubble: meshes drawn with
    /// one material, thrown and bounced off the terrain, then shrunk away.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Effects + "Debris Set", fileName = "DebrisSet")]
    public class DebrisSet : ScriptableObject
    {
        [Tooltip("Meshes picked at random per piece, such as rubble chunks or a unit cube scaled into a sandbag.")]
        public List<Mesh> meshes = new();

        [Tooltip("Material every piece draws with, usually the prop's own.")]
        public Material material;

        [Tooltip("Scale per axis applied to the mesh, varied 20 % either way per piece.")]
        public Vector3 size = Vector3.one;

        [Tooltip("Pieces thrown at High effects; Medium throws fewer and Low none.")]
        [Min(1)]
        public int count = 6;

        [Tooltip("Launch speed range in m/s, mostly up and out.")]
        public Vector2 speed = new(3f, 6f);

        [Tooltip("Seconds a piece lies before it is gone.")]
        public Vector2 life = new(10f, 20f);

        /// <summary>Why the set can't throw, or null.</summary>
        public string Problem()
        {
            if (material == null)
            {
                return $"Debris set '{name}' has no 'material'.";
            }

            if (meshes.Count == 0 || meshes.Contains(null))
            {
                return $"Debris set '{name}' needs 'meshes' with none empty.";
            }

            return null;
        }
    }
}
