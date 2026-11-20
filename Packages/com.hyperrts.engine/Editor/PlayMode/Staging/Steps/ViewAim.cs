using System;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>The JSON of one end of a <see cref="ViewStep"/>: an aim whose gaps an end aim fills from the start aim.</summary>
    internal sealed class ViewAim
    {
        public float[] at;
        public float? lift;
        public float? distance;
        public float? pitch;
        public float? yaw;

        /// <summary>The aim with each gap taken from <paramref name="kept"/>; a start aim has none to keep and needs its distance, pitch and yaw.</summary>
        public CameraAim Aim(float2 look, float defaultLift, CameraAim? kept, string key)
        {
            float Pick(float? given, float? fromKept, string field) =>
                given ?? fromKept ?? throw new InvalidOperationException($"needs '{field}' in '{key}'");

            return new CameraAim(
                at == null ? kept?.Look ?? look : StagingStep.Point(at, $"{key}.at"),
                lift ?? defaultLift,
                Pick(distance, kept?.Distance, "distance"),
                Pick(pitch, kept?.Pitch, "pitch"),
                Pick(yaw, kept?.Yaw, "yaw"));
        }
    }
}
