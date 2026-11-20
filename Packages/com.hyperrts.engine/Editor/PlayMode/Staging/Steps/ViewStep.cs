using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;
using Newtonsoft.Json;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>
    /// Takes the camera off the player for a cinematic move: from one aim to another over some seconds, or a held aim,
    /// at a group (followed as it moves) or a map point. With <c>sun</c>, yaws count from the way the sunlight falls, so
    /// yaw 0 keeps the sun behind the camera on any map. A later camera step hands the camera back.
    /// </summary>
    internal sealed class ViewStep : StagingStep
    {
        public string on;
        public float[] at;
        public float? lift;
        [JsonProperty("from")]
        public ViewAim start;
        [JsonProperty("to")]
        public ViewAim end;
        public float? seconds;
        public float? fov;
        public bool? sun;

        private CameraAim _from;
        private CameraAim _to;

        public override IEnumerable<string> Uses => on == null ? base.Uses : new[] { on };

        public override void Check()
        {
            var target = OneOf(("on", on != null), ("at", at != null));
            var look = target == "at" ? Point(at, "at") : float2.zero;
            _from = (start ?? throw new InvalidOperationException("needs 'from'")).Aim(look, lift ?? 0f, null, "from");
            _to = end == null ? _from : end.Aim(look, _from.Lift, _from, "to");
            if (end != null && (seconds ?? 0f) <= 0f)
            {
                throw new InvalidOperationException($"needs 'seconds' above 0 to move, got {seconds}");
            }
        }

        public override void Run(StagingRun run)
        {
            if (!run.Camera.TryGetComponent(out CinematicCamera cinematic))
            {
                cinematic = run.Camera.gameObject.AddComponent<CinematicCamera>();
            }

            if (fov is { } angle)
            {
                run.Camera.fieldOfView = angle;
            }

            run.Follow = null;
            var group = on == null ? null : run.FindGroup(on);
            Func<float2?> track = group == null ? null : () => group.TryGetCentre(run.World, out var centre) ? centre : null;
            var turn = sun == true ? SunHeading() : 0f;
            cinematic.Path = new ViewPath(_from.Turned(turn), _to.Turned(turn), Time.time, end == null ? 0f : seconds.Value, point => run.GroundPoint(point).y, track);
        }

        // The heading the brightest directional light shines along, as a camera yaw.
        private static float SunHeading()
        {
            var sun = UnityEngine.Object.FindObjectsByType<Light>()
                .Where(light => light.type == LightType.Directional && light.isActiveAndEnabled)
                .OrderByDescending(light => light.intensity)
                .FirstOrDefault();
            return sun != null
                ? sun.transform.eulerAngles.y
                : throw new InvalidOperationException("HyperRTS: a view with 'sun' needs a directional light in the scene.");
        }
    }
}
