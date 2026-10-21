using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Replays;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Impacts
{
    /// <summary>
    /// Lays the frame's impact <see cref="CueEvent"/>s on open ground as their shooter type's <see cref="ImpactMark"/>s:
    /// pooled URP decal projectors at terrain height, along the ground's slope, turned and sized at random, fading
    /// with age; past a kind's cap the oldest is reused. Craters and scorch also scar the interaction maps. Skips
    /// impacts the local player can't see. During a replay, marks age on its clock and clear when it seeks back.
    /// </summary>
    // After InteractionStampSystem, which clears the stamps craters join.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InteractionStampSystem))]
    public partial class ImpactMarkSystem : SystemBase
    {
        private readonly Dictionary<ImpactMark, ImpactMarkDecals> _decals = new();
        private TypeCatalog<ImpactMark[]> _catalog;
        private CueVisibility _visibility;
        private GameObject _root;
        private InteractionStampSystem _stampSystem;
        private float _clock = float.NegativeInfinity;
        private Unity.Mathematics.Random _random = new(0x9E3779B9u);

        /// <summary>The marks of an entity with <see cref="EntityImpactMark"/>s.</summary>
        public static ImpactMark[] ReadMarks(EntityManager entityManager, Entity source)
        {
            var entries = entityManager.GetBuffer<EntityImpactMark>(source, true);
            var marks = new ImpactMark[entries.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                marks[i] = entries[i].Mark.Value;
            }

            return marks;
        }

        protected override void OnCreate()
        {
            _catalog = new TypeCatalog<ImpactMark[]>(SystemAPI.QueryBuilder().WithAll<EntityInfo, EntityImpactMark>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build(), ReadMarks);
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<CueQueue>();
            RequireForUpdate<PrefabRegistry>();
        }

        // The projectors are DontSave, so they outlive scene reloads and must go with the world.
        protected override void OnDestroy() => CoreUtils.Destroy(_root);

        protected override void OnUpdate()
        {
            CompleteDependency();
            var now = Clock();
            var scars = Scars();
            if (now < _clock)
            {
                Clear();
                if (scars != null)
                {
                    scars.Reset = true;
                }
            }

            _clock = now;
            PlaceNew(now, scars);
            foreach (var decals in _decals.Values)
            {
                decals.Fade(now);
            }
        }

        /// <summary>Replay time during playback, so pausing holds the fades and seeking can be told apart.</summary>
        private float Clock() =>
            SystemAPI.TryGetSingleton(out ReplayPlayback replay) ? replay.Time : (float)SystemAPI.Time.ElapsedTime;

        private InteractionStamps Scars() => InteractionMap.Quality == InteractionQuality.Off
            ? null
            : (_stampSystem ??= World.GetExistingSystemManaged<InteractionStampSystem>())?.Stamps;

        private void PlaceNew(float now, InteractionStamps scars)
        {
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (cues.IsEmpty)
            {
                return;
            }

            _visibility.Begin();
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            foreach (var impact in cues)
            {
                if (!CueRules.IsGroundImpact(impact) || !_visibility.IsSeen(impact))
                {
                    continue;
                }

                var marks = _catalog.Find(EntityManager, registry, impact.TypeId);
                for (var i = 0; marks != null && i < marks.Length; i++)
                {
                    Place(marks[i], impact, terrain, now, scars);
                }
            }
        }

        private void Place(ImpactMark mark, in CueEvent impact, in TerrainHeight terrain, float now, InteractionStamps scars)
        {
            if (mark == null)
            {
                return;
            }

            var decal = mark.decals.Length > 0 ? mark.decals[_random.NextInt(mark.decals.Length)] : null;
            var diameter = ImpactMarkMath.Diameter(mark, impact.Radius, _random.NextFloat());
            var point = impact.Position.xz;
            if (scars != null && mark.ScarsGround)
            {
                scars.AddCrater(point, mark, diameter);
            }

            if (decal == null)
            {
                return;
            }

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
