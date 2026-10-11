using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Replays;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Impacts
{
    /// <summary>
    /// Lays the frame's <see cref="ImpactEvent"/>s on open ground as their shooter type's <see cref="ImpactMark"/>s:
    /// pooled URP decal projectors at terrain height, along the ground's slope, turned and sized at random, fading
    /// with age; past a kind's cap the oldest is reused. Skips impacts the local player can't see. During a replay,
    /// marks age on its clock and clear when it seeks back.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ImpactMarkSystem : SystemBase
    {
        private readonly Dictionary<ImpactMark, ImpactMarkDecals> _decals = new();
        private ImpactMarkCatalog _catalog;
        private EntityQuery _fog;
        private GameObject _root;
        private float _clock = float.NegativeInfinity;
        private Unity.Mathematics.Random _random = new(0x9E3779B9u);

        protected override void OnCreate()
        {
            _catalog = new ImpactMarkCatalog(SystemAPI.QueryBuilder().WithAll<EntityInfo, EntityImpactMark>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build());
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            RequireForUpdate<ImpactQueue>();
            RequireForUpdate<PrefabRegistry>();
        }

        // The projectors are DontSave, so they outlive scene reloads and must go with the world.
        protected override void OnDestroy()
        {
            if (_root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_root);
            }
            else
            {
                Object.DestroyImmediate(_root);
            }
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var now = Clock();
            if (now < _clock)
            {
                Clear();
            }

            _clock = now;
            PlaceNew(now);
            foreach (var decals in _decals.Values)
            {
                decals.Fade(now);
            }
        }

        /// <summary>Replay time during playback, so pausing holds the fades and seeking can be told apart.</summary>
        private float Clock() =>
            SystemAPI.TryGetSingleton(out ReplayPlayback replay) ? replay.Time : (float)SystemAPI.Time.ElapsedTime;

        private void PlaceNew(float now)
        {
            var impacts = SystemAPI.GetSingletonBuffer<ImpactEvent>(true);
            if (impacts.IsEmpty)
            {
                return;
            }

            var view = SystemAPI.TryGetSingleton(out LocalFogView local) ? local : default;
            SystemAPI.TryGetSingleton(out FactionRelations relations);
            _fog.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            foreach (var impact in impacts)
            {
                var seen = !view.Active || ImpactRules.IsVisible(impact, view.Viewer, fog, relations);
                if (impact.Surface != ImpactSurface.Ground || !seen)
                {
                    continue;
                }

                var marks = _catalog.Find(EntityManager, registry, impact.TypeId);
                for (var i = 0; marks != null && i < marks.Length; i++)
                {
                    Place(marks[i], impact, terrain, now);
                }
            }
        }

        private void Place(ImpactMark mark, in ImpactEvent impact, in TerrainHeight terrain, float now)
        {
            var decal = mark != null && mark.decals.Length > 0 ? mark.decals[_random.NextInt(mark.decals.Length)] : null;
            if (decal == null)
            {
                return;
            }

            var diameter = ImpactMarkMath.Diameter(mark, impact.Radius, _random.NextFloat());
            var point = impact.Position.xz;
            var ground = new float3(point.x, terrain.Height(point), point.y);
            var normal = ImpactMarkMath.GroundNormal(terrain, point, math.max(diameter * 0.25f, 0.5f));
            var rotation = ImpactMarkMath.Facing(normal, _random.NextFloat(2f * math.PI));
            Decals(mark).Place(now, decal, ground, rotation, diameter, _random.NextBool());
        }

        private ImpactMarkDecals Decals(ImpactMark mark)
        {
            if (_decals.TryGetValue(mark, out var decals))
            {
                return decals;
            }

            if (_root == null)
            {
                _root = new GameObject("HyperRTS Impact Marks") { hideFlags = HideFlags.DontSave };
            }

            return _decals[mark] = new ImpactMarkDecals(mark, _root.transform);
        }

        private void Clear()
        {
            foreach (var decals in _decals.Values)
            {
                decals.Clear();
            }
        }
    }
}
