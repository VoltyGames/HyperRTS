using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Sets the global _VATTime that vertex animation clips play against from the world clock, so pausing or speeding up
    /// a match or replay does the same to them. <see cref="VATClip"/> start times use this clock.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct VATClockSystem : ISystem
    {
        private static readonly int TimeId = Shader.PropertyToID("_VATTime");

        public void OnUpdate(ref SystemState state) => Shader.SetGlobalFloat(TimeId, (float)SystemAPI.Time.ElapsedTime);
    }
}
