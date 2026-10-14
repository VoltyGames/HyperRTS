using HyperRTS.Core;
using Unity.Entities;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Owns the world's <see cref="EffectPool"/>: ticks it first each frame, frees it with the world.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class EffectPoolSystem : SystemBase
    {
        public EffectPool Pool { get; } = new();

        /// <summary>The share of authored particles to emit at the current detail.</summary>
        public static float Density => EffectPool.Density(EffectDetail.Level);

        // The rigs are DontSave, so they outlive scene reloads and must go with the world.
        protected override void OnDestroy() => Pool.Dispose();

        protected override void OnUpdate() => Pool.Tick(SystemAPI.Time.DeltaTime);
    }
}
