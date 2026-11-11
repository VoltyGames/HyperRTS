using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.PlayMode;
using HyperRTS.Editor.Validation.Project;
using HyperRTS.Presentation.Environment.Vegetation.Grass;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Death;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>
    /// The features a cost probe turns off in turn, each by the switch the engine already has. A feature the scene
    /// lacks is left out and named in <see cref="Missing"/>.
    /// </summary>
    internal sealed class MapLookSwitches : IDisposable
    {
        // Above every weather and battle haze volume.
        private const float VolumePriority = float.MaxValue;

        private readonly Dictionary<string, Action<bool>> _switches = new();
        private Volume _noFog;

        public MapLookSwitches(Camera camera, Material water)
        {
            AddBehaviours<GrassRenderer>("grass");
            AddBehaviours<RowCropRenderer>("rowCrops");
            AddTrees();
            AddContactShadows();
            AddFog();
            AddWater(water);
            AddAntialiasing(camera);
        }

        public IEnumerable<string> Names => _switches.Keys;

        public List<string> Missing { get; } = new();

        public void Set(string feature, bool on) => _switches[feature](on);

        /// <summary>Turns every feature back on, since a failure can leave one off mid-measurement.</summary>
        public void Dispose()
        {
            foreach (var set in _switches.Values)
            {
                set(true);
            }

            if (_noFog != null)
            {
                Object.Destroy(_noFog.sharedProfile);
                Object.Destroy(_noFog.gameObject);
            }
        }

        private void Add(string feature, bool found, Action<bool> set)
        {
            if (found)
            {
                _switches[feature] = set;
            }
            else
            {
                Missing.Add(feature);
            }
        }

        private void AddBehaviours<T>(string feature) where T : Behaviour
        {
            var found = Object.FindObjectsByType<T>();
            Add(feature, found.Length > 0, on =>
            {
                foreach (var behaviour in found)
                {
                    behaviour.enabled = on;
                }
            });
        }

        // The tree entities (the ones that topple) with all their parts, plus the terrain's own trees.
        private void AddTrees()
        {
            var trees = FindTrees();
            var terrain = Terrain.activeTerrain;
            Add("trees", trees.Count > 0 || terrain != null, on =>
            {
                if (terrain != null)
                {
                    terrain.drawTreesAndFoliage = on;
                }

                if (trees.Count == 0)
                {
                    return;
                }

                PlayWorld.TryGetPresented(out var manager);
                using var entities = new NativeArray<Entity>(trees.Where(manager.Exists).ToArray(), Allocator.Temp);
                if (on)
                {
                    manager.RemoveComponent<DisableRendering>(entities);
                }
                else
                {
                    manager.AddComponent<DisableRendering>(entities);
                }
            });
        }

        private static List<Entity> FindTrees()
        {
            var trees = new List<Entity>();
            if (PlayWorld.TryGetPresented(out var entityManager))
            {
                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Topple>(), ComponentType.ReadOnly<SceneryId>());
                foreach (var root in query.ToEntityArray(Allocator.Temp))
                {
                    trees.Add(root);
                    if (entityManager.HasBuffer<LinkedEntityGroup>(root))
                    {
                        trees.AddRange(entityManager.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
                    }
                }
            }

            return trees;
        }

        private void AddContactShadows()
        {
            var features = RendererFeatures.ActiveRenderers()
                .Select(RendererFeatures.Find<ContactShadowsFeature>).Where(feature => feature != null).ToList();
            Add("contactShadows", features.Count > 0, on =>
            {
                foreach (var feature in features)
                {
                    feature.SetActive(on);
                }
            });
        }

        // A top-priority volume that overrides both of URP's fog kinds off.
        private void AddFog()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var fog = profile.Add<FogVolumeComponent>(true);
            fog.analyticFogEnabled.Override(false);
            fog.volumetricFogEnabled.Override(false);
            _noFog = new GameObject("Look Cost No Fog").AddComponent<Volume>();
            _noFog.isGlobal = true;
            _noFog.priority = VolumePriority;
            _noFog.sharedProfile = profile;
            _noFog.enabled = false;
            _switches["fog"] = on => _noFog.enabled = !on;
        }

        // The river and pond surfaces, drawn with the climate's water material.
        private void AddWater(Material water)
        {
            var renderers = Object.FindObjectsByType<MeshRenderer>()
                .Where(renderer => renderer.sharedMaterial == water).ToList();
            Add("water", renderers.Count > 0, on =>
            {
                foreach (var renderer in renderers)
                {
                    renderer.enabled = on;
                }
            });
        }

        private void AddAntialiasing(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            var mode = data.antialiasing;
            Add("antialiasing", mode != AntialiasingMode.None, on =>
                data.antialiasing = on ? mode : AntialiasingMode.None);
        }
    }
}
