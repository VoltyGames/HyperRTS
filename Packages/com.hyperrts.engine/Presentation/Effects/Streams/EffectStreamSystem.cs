using HyperRTS.Presentation.Effects.Rig;
using Unity.Entities;

namespace HyperRTS.Presentation.Effects.Streams
{
    /// <summary>
    /// A system that streams effects through <see cref="EffectStreams"/>, stopping them all once it stops updating so
    /// the last entity's stream never lingers where it went.
    /// </summary>
    public abstract partial class EffectStreamSystem : SystemBase
    {
        protected EffectPool Pool { get; private set; }
        protected EffectStreams Streams { get; private set; }

        protected override void OnCreate()
        {
            Pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            Streams = new EffectStreams(Pool);
        }

        protected override void OnStopRunning() => Streams.StopAll();
    }
}
