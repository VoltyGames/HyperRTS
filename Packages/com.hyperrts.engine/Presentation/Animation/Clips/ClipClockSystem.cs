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
        private static readonly int LastTimeId = Shader.PropertyToID("_LastClipTime");

        // Last frame's time lets LitTeam's motion vector pass pose a vertex where it was a frame ago.
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time;
            Shader.SetGlobalFloat(TimeId, (float)time.ElapsedTime);
            Shader.SetGlobalFloat(LastTimeId, (float)(time.ElapsedTime - time.DeltaTime));
        }
    }
}
