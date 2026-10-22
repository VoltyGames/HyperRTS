using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Publishes this frame's <see cref="LocalGust"/>s to the vegetation shaders: downwash under low rotors the local
    /// player sees and the blasts of seen impacts. Off with the interaction maps.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LocalGustSystem : SystemBase
    {
        private static readonly int GustsId = Shader.PropertyToID("_HyperRTSGusts");
        private static readonly int CountId = Shader.PropertyToID("_HyperRTSGustCount");

        // Smaller splashes, such as grenades on a soldier, push no gust worth drawing.
        private const float MinBlastRadius = 1f;

        private readonly List<LocalGust> _blasts = new();
        private readonly Vector4[] _packed = new Vector4[LocalGust.Max];
        private CueVisibility _visibility;

        protected override void OnCreate() => _visibility = new CueVisibility(ref CheckedStateRef);

        protected override void OnDestroy() => Shader.SetGlobalFloat(CountId, 0f);

        protected override void OnUpdate()
        {
            if (InteractionMap.Quality == InteractionQuality.Off)
            {
                _blasts.Clear();
                Shader.SetGlobalFloat(CountId, 0f);
                return;
            }

            CompleteDependency();
            AgeBlasts(SystemAPI.Time.DeltaTime);
            AddBlasts();
            var count = AddDownwash(0);
            for (var i = _blasts.Count - 1; i >= 0 && count < LocalGust.Max; i--)
            {
                _packed[count++] = _blasts[i].Packed;
            }

            Shader.SetGlobalVectorArray(GustsId, _packed);
            Shader.SetGlobalFloat(CountId, count);
        }

        private void AgeBlasts(float deltaTime)
        {
            for (var i = _blasts.Count - 1; i >= 0; i--)
            {
                var blast = _blasts[i];
                blast.Advance(deltaTime);
                if (blast.Expired)
                {
                    _blasts.RemoveAt(i);
                    continue;
                }

                _blasts[i] = blast;
            }
        }

        private void AddBlasts()
        {
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true) || cues.IsEmpty)
            {
                return;
            }

            _visibility.Begin();
            foreach (var cue in cues)
            {
                var blast = cue.Slot == CueSlot.Impact && cue.Radius >= MinBlastRadius;
                if (!blast || !_visibility.IsSeen(cue))
                {
                    continue;
                }

                // Only the newest reach the shader, so a barrage keeps no more.
                if (_blasts.Count == LocalGust.Max)
                {
                    _blasts.RemoveAt(0);
                }

                _blasts.Add(LocalGust.Blast(cue.Position.xz, cue.Radius));
            }
        }

        /// <summary>Packs low rotors from <paramref name="count"/> on; returns the new count.</summary>
        private int AddDownwash(int count)
        {
            // No terrain is a flat world at height 0 (TerrainHeight's default).
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            var washes = SystemAPI.GetComponentLookup<RotorWash>(true);
            foreach (var (spin, place, entity) in SystemAPI.Query<RefRO<RotorSpin>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside>().WithEntityAccess())
            {
                var position = place.ValueRO.Position;
                var reach = washes.TryGetComponent(entity, out var wash) ? wash.Height : RotorWash.DefaultHeight;
                var height = position.y - terrain.Height(position.xz);
                var gust = LocalGust.Downwash(position.xz, height, spin.ValueRO.Amount, reach);
                if (gust.Push > 0f && count < LocalGust.Max)
                {
                    _packed[count++] = gust.Packed;
                }
            }

            return count;
        }
    }
}
