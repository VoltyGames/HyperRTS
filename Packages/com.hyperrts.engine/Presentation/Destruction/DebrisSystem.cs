using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Throws each appearing ruin's <see cref="Debris"/>, flies, bounces and draws every piece in its
    /// <see cref="DebrisField"/>, and keeps settled pieces in <see cref="LastingDebris"/>. Medium effects throw fewer
    /// pieces and keep fewer, Low none.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DebrisSystem : SystemBase
    {
        public DebrisField Field { get; } = new();

        public LastingDebris Lasting { get; private set; }

        protected override void OnCreate() => Lasting = new LastingDebris(EffectDetail.LastingDebrisAt(EffectDetail.Level));

        protected override void OnDestroy() => Lasting.Dispose();

        protected override void OnUpdate()
        {
            CompleteDependency();
            foreach (var (debris, pending, place) in SystemAPI
                         .Query<RefRO<Debris>, EnabledRefRW<Debris>, RefRO<LocalToWorld>>())
            {
                pending.ValueRW = false;
                Throw(debris.ValueRO, place.ValueRO.Position, float3.zero);
            }

            KeepCapForDetail();
            Lasting.Step(SystemAPI.Time.DeltaTime);
            if (Field.Count == 0)
            {
                return;
            }

            // No terrain is a flat world at height 0 (TerrainHeight's default).
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            Field.Step(SystemAPI.Time.DeltaTime, terrain, Lasting);
            Field.Draw();
        }

        /// <summary>Throws <paramref name="debris"/> from <paramref name="center"/>, leaning toward <paramref name="outward"/>.</summary>
        public void Throw(in Debris debris, float3 center, float3 outward)
        {
            var set = debris.Set.Value;
            var count = PieceCount(set.count, EffectDetail.Level);
            if (count > 0)
            {
                Field.Throw(set, center, outward, debris.Radius, count, math.hash(center));
            }
        }

        public static int PieceCount(int authored, DetailLevel level) =>
            level == DetailLevel.Low ? 0 : (int)math.ceil(authored * EffectDetail.DensityAt(level));

        // A changed effect detail starts the lasting layer over at its new cap.
        private void KeepCapForDetail()
        {
            var cap = EffectDetail.LastingDebrisAt(EffectDetail.Level);
            if (cap != Lasting.Cap)
            {
                Lasting.Dispose();
                Lasting = new LastingDebris(cap);
            }
        }
    }
}
