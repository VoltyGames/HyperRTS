using System;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Which ground burns and how, for <see cref="FieldFireSystem"/>: shells that land on dry stubble, grass or ripe
    /// crops set fires that spread with the wind and leave ash. Client visuals only; the fires harm nothing.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Environment + "Field Fires")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class FieldFireSettings : PresentationBehaviour
    {
        // Rows of the alphamap read at once, so a large terrain never copies all its weights in one go.
        private const int AlphamapRows = 64;

        [Tooltip("Effect rig streamed over each cluster of burning cells: low flame along the ground and smoke.")]
        public GameObject rig;

        [Tooltip("Terrain layers dry enough to burn. A cell burns where these hold over half the ground's weight.")]
        public TerrainLayer[] dryLayers = Array.Empty<TerrainLayer>();

        [Tooltip("Fires only start while the ground's wetness is below this.")]
        [Range(0f, 1f)]
        public float wetnessLimit = 0.3f;

        [Tooltip("Smallest splash radius in metres that lights dry ground, so only high explosive does.")]
        [Min(0f)]
        public float smallestBlast = 1.5f;

        [Tooltip("Chance that a blast on dry ground lights it.")]
        [Range(0f, 1f)]
        public float igniteChance = 0.4f;

        [Tooltip("Seconds one cell burns, min and max.")]
        public Vector2 burnSeconds = new(20f, 60f);

        [Tooltip("Metres one shell's fire may spread from where it started, min and max; it dies out well inside.")]
        public Vector2 reach = new(5f, 15f);

        [Tooltip("Metres round every building's footprint that never burn, so bases and pads stay clear.")]
        [Min(0f)]
        public float buildingClearance = 20f;

        [Tooltip("Fire effects playing at once, nearest the camera first.")]
        [Min(0)]
        public int maxEffects = 16;

        /// <summary>The enabled instance in the loaded scenes, or null.</summary>
        [AutoStaticsCleanup]
        public static FieldFireSettings Active { get; private set; }

        /// <summary>Throws naming the first field that can't be used.</summary>
        public void Require()
        {
            Require(rig != null, nameof(rig), "is missing");
            Require(dryLayers is { Length: > 0 }, nameof(dryLayers), "is empty");
            Require(burnSeconds.x > 0f && burnSeconds.y >= burnSeconds.x, nameof(burnSeconds), "needs 0 < min <= max");
            Require(reach.x > 0f && reach.y >= reach.x, nameof(reach), "needs 0 < min <= max");
        }

        /// <summary>
        /// Whether an impact lights the ground it hit: a big enough blast on open ground, below the wetness limit, and
        /// the impact's own <paramref name="seed"/> under the ignite chance.
        /// </summary>
        public bool Lights(in CueEvent impact, float wetness, uint seed)
        {
            var blast = impact.Slot == CueSlot.Impact && impact.Radius >= smallestBlast;
            var dryGround = impact.Surface == ImpactSurface.Ground && wetness < wetnessLimit;
            return blast && dryGround && (seed >> 8) * (1f / (1 << 24)) < igniteChance;
        }

        /// <summary>Marks dry every cell whose terrain is mostly <see cref="dryLayers"/> and isn't water.</summary>
        public void MarkDry(FieldFires fires, Terrain terrain, in GroundMaterials ground)
        {
            var data = terrain.terrainData;
            var resolution = data.alphamapResolution;
            var dry = DryTexels(data);
            var origin = ((float3)terrain.GetPosition()).xz;
            var perMetre = resolution / ((float3)data.size).xz;
            for (var z = 0; z < fires.Size.y; z++)
            {
                for (var x = 0; x < fires.Size.x; x++)
                {
                    var centre = fires.Centre(new int2(x, z));
                    var texel = (int2)math.floor((centre - origin) * perMetre);
                    var onTerrain = math.all(texel >= 0) && math.all(texel < resolution);
                    if (onTerrain && dry[texel.x + texel.y * resolution] && ground.At(centre) != SurfaceMaterial.Water)
                    {
                        fires.MarkDry(new int2(x, z));
                    }
                }
            }
        }

        private void OnEnable() => Active = this;

        protected override void OnDisabled()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        // Whether each alphamap texel is over half dry layers, row-major from the terrain's south-west corner.
        private bool[] DryTexels(TerrainData data)
        {
            var resolution = data.alphamapResolution;
            var burns = Array.ConvertAll(data.terrainLayers, layer => Array.IndexOf(dryLayers, layer) >= 0);
            var dry = new bool[resolution * resolution];
            for (var row = 0; row < resolution; row += AlphamapRows)
            {
                var rows = Math.Min(AlphamapRows, resolution - row);
                var weights = data.GetAlphamaps(0, row, resolution, rows);
                for (var z = 0; z < rows; z++)
                {
                    for (var x = 0; x < resolution; x++)
                    {
                        var share = 0f;
                        for (var layer = 0; layer < burns.Length; layer++)
                        {
                            share += burns[layer] ? weights[z, x, layer] : 0f;
                        }

                        dry[x + (row + z) * resolution] = share > 0.5f;
                    }
                }
            }

            return dry;
        }

        private void Require(bool valid, string field, string problem)
        {
            if (!valid)
            {
                throw new InvalidOperationException($"FieldFireSettings '{name}': {field} {problem}.");
            }
        }
    }
}
