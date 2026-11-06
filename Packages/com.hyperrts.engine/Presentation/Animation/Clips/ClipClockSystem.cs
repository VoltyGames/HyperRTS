using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>
    /// Sets the global _ClipTime that animation texture clips play against from the world clock, so pausing or speeding
    /// up a match or replay does the same to them. <see cref="PlayingClip"/> start times use this clock.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ClipClockSystem : ISystem
    {
        private static readonly int TimeId = Shader.PropertyToID("_ClipTime");

        public void OnUpdate(ref SystemState state) => Shader.SetGlobalFloat(TimeId, (float)SystemAPI.Time.ElapsedTime);
    }
}
