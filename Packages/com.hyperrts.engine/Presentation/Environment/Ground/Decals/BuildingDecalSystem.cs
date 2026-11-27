using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.ImpactMarks;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Environment.Ground.Decals
{
    /// <summary>
    /// Shows each <see cref="BuildingDecal"/> as a pooled URP decal projector while its building stands finished and the
    /// local player sees it, and takes them back when it is fogged, dies or goes.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BuildingDecalSystem : SystemBase
    {
        private readonly Dictionary<Entity, List<DecalProjector>> _shown = [];
        private readonly HashSet<Entity> _standing = [];
        private readonly List<Entity> _gone = [];
        private readonly Stack<DecalProjector> _free = new();
        private GameObject _root;

        // The projectors are DontSave, so they outlive scene reloads and must go with the world.
        protected override void OnDestroy() => CoreUtils.Destroy(_root);

        protected override void OnUpdate()
        {
            _standing.Clear();
            foreach (var (decals, place, building) in SystemAPI.Query<DynamicBuffer<BuildingDecal>, RefRO<LocalToWorld>>()
                         .WithNone<ConstructionProgress, Dead, FogHidden>().WithEntityAccess())
            {
                _standing.Add(building);
                if (!_shown.ContainsKey(building))
                {
                    _shown.Add(building, Show(decals, place.ValueRO));
                }
            }

            HideGone();
        }

        private List<DecalProjector> Show(DynamicBuffer<BuildingDecal> decals, in LocalToWorld place)
        {
            var projectors = new List<DecalProjector>(decals.Length);
            foreach (var decal in decals)
            {
                var projector = Take();
                projector.material = decal.Material.Value;
                projector.size = decal.Size;
                projector.transform.SetPositionAndRotation(math.transform(place.Value, decal.Position),
                    math.mul(place.Rotation, decal.Facing));
                projector.enabled = true;
                projectors.Add(projector);
            }

            return projectors;
        }

        private void HideGone()
        {
            _gone.Clear();
            foreach (var building in _shown.Keys)
            {
                if (!_standing.Contains(building))
                {
                    _gone.Add(building);
                }
            }

            foreach (var building in _gone)
            {
                foreach (var projector in _shown[building])
                {
                    projector.enabled = false;
                    _free.Push(projector);
                }

                _shown.Remove(building);
            }
        }

        /// <summary>A free projector, switched off so it registers with the decal system once placed.</summary>
        private DecalProjector Take()
        {
            if (_free.TryPop(out var free))
            {
                return free;
            }

            if (_root == null)
            {
                _root = new GameObject("HyperRTS Building Decals") { hideFlags = HideFlags.DontSave };
            }

            var go = new GameObject("Building Decal") { hideFlags = HideFlags.DontSave };
            go.SetActive(false);
            go.transform.SetParent(_root.transform, false);
            var projector = go.AddComponent<DecalProjector>();
            projector.enabled = false;
            projector.pivot = Vector3.zero;
            projector.startAngleFade = ImpactMarkDecals.StartAngleFade;
            projector.endAngleFade = ImpactMarkDecals.EndAngleFade;
            go.SetActive(true);
            return projector;
        }
    }
}
