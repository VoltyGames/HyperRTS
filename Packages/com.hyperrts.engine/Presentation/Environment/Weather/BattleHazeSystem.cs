using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>
    /// Feeds the <see cref="BattleHaze"/> from impacts the local player sees (by splash), burning wrecks, damaged
    /// buildings and collapses, steps it with the wind and hands its texture to the volumetric fog.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SpawnEffectSystem))]
    public partial class BattleHazeSystem : SystemBase
    {
        private const float StepSeconds = 0.25f;

        // Smoke trails the wind that carries it.
        private const float DriftPerStrength = 2f;

        private const float ImpactReachPerRadius = 2f;
        private const float ImpactHazePerRadius = 0.1f;
        private const float WreckReach = 6f;
        private const float WreckHazePerSecond = 0.02f;
        private const float BuildingReach = 10f;
        private const float BuildingHazePerSecond = 0.03f;
        private const float CollapseReach = 15f;
        private const float CollapseHaze = 1.5f;

        private CueVisibility _visibility;
        private MapSettings _area;
        private readonly BattleHazeFog _fog = new();
        private Texture3D _texture;
        private float _clock;

        /// <summary>This map's haze; null while it is off or no map is loaded.</summary>
        public BattleHaze Haze { get; private set; }

        protected override void OnCreate()
        {
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<MapSettings>();
        }

        protected override void OnStopRunning() => Clear();

        protected override void OnDestroy() => Clear();

        protected override void OnUpdate()
        {
            if (!PresentationQuality.Current.BattleHaze)
            {
                Clear();
                return;
            }

            CompleteDependency();
            Prepare(SystemAPI.GetSingleton<MapSettings>());
            var deltaTime = SystemAPI.Time.DeltaTime;
            AddImpacts();
            AddSmoke(deltaTime);
            AddCollapses();

            _clock += deltaTime;
            if (_clock < StepSeconds)
            {
                return;
            }

            // No Wind in the scene means still air.
            SystemAPI.TryGetSingleton(out WindState wind);
            Haze.Advance(_clock, wind.Push * DriftPerStrength);
            _clock = 0f;
            _texture.SetPixelData(Haze.Texels, 0);
            _texture.Apply(false);
        }

        // A new map, or one whose area changed, starts clear air.
        private void Prepare(in MapSettings map)
        {
            if (Haze != null && _area.SameArea(map))
            {
                return;
            }

            Clear();
            _area = map;
            Haze = new BattleHaze(map.Min, math.cmax(map.Size));
            _texture = new Texture3D(BattleHaze.Cells, BattleHaze.Layers, BattleHaze.Cells,
                GraphicsFormat.R16_SFloat, TextureCreationFlags.None)
            {
                name = "Battle Haze",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            _texture.SetPixelData(Haze.Texels, 0);
            _texture.Apply(false);
            _fog.Show(_texture, Haze.Tiling);
        }

        private void AddImpacts()
        {
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true) || cues.IsEmpty)
            {
                return;
            }

            _visibility.Begin();
            foreach (var cue in cues)
            {
                if (cue.IsBlast && _visibility.IsSeen(cue))
                {
                    Haze.Add(cue.Position.xz, cue.Radius * ImpactReachPerRadius, cue.Radius * ImpactHazePerRadius);
                }
            }
        }

        private void AddSmoke(float deltaTime)
        {
            foreach (var (decay, place) in SystemAPI.Query<RefRO<Decay>, RefRO<LocalToWorld>>()
                         .WithAll<LoopEffect>().WithNone<FogHidden, Inside>())
            {
                var standing = 1f - decay.ValueRO.DespawnProgress;
                Haze.Add(place.ValueRO.Position.xz, WreckReach, WreckHazePerSecond * standing * deltaTime);
            }

            foreach (var (stages, shown, place) in SystemAPI
                         .Query<DynamicBuffer<DamageStage>, RefRO<ShownDamageStage>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, ConstructionProgress>())
            {
                var damage = stages.Length > 1 ? math.max(shown.ValueRO.Index, 0) / (stages.Length - 1f) : 0f;
                if (damage > 0f)
                {
                    Haze.Add(place.ValueRO.Position.xz, BuildingReach, BuildingHazePerSecond * damage * deltaTime);
                }
            }
        }

        // Read before SpawnEffectSystem plays and clears each ruin's collapse; a falling tree raises no smoke.
        private void AddCollapses()
        {
            foreach (var place in SystemAPI.Query<RefRO<LocalToWorld>>()
                         .WithAll<SpawnEffect>().WithNone<ToppleFall, FogHidden>())
            {
                Haze.Add(place.ValueRO.Position.xz, CollapseReach, CollapseHaze);
            }
        }

        private void Clear()
        {
            _fog.Hide();
            CoreUtils.Destroy(_texture);
            _texture = null;
            Haze?.Dispose();
            Haze = null;
            _clock = 0f;
        }
    }
}
