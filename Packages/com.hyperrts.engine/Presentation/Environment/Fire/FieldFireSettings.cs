using System;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
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

        private World _world;
        private Entity _source;

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
            return blast && dryGround && Hashes.Fraction(seed) < igniteChance;
        }

        // Retried every frame: the world may not exist yet, or may be a new one.
        private void Update()
        {
            if (!MatchView.TryGetDefault(out var view))
            {
                return;
            }

            var manager = view.EntityManager;
            if (_world == manager.World && manager.Exists(_source))
            {
                return;
            }

            _world = manager.World;
            _source = manager.CreateEntity(typeof(FieldFireSource));
            manager.SetComponentData(_source, new FieldFireSource { Settings = this });
        }

        protected override void OnDisabled()
        {
            if (_world is { IsCreated: true } && _world.EntityManager.Exists(_source))
            {
                _world.EntityManager.DestroyEntity(_source);
            }

            _world = null;
            _source = Entity.Null;
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
