using System;
using System.Collections.Generic;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// Takes the camera off the player for a cinematic move: from one aim to another over some seconds, or a held aim,
    /// at a group (followed as it moves) or a map point. A later camera step hands the camera back.
    /// </summary>
    internal sealed class GlideStep : StagingStep
    {
        private readonly string _on;
        private readonly CameraAim _from;
        private readonly CameraAim _to;
        private readonly float _seconds;
        private readonly float? _fov;

        private GlideStep(StagingObject json)
        {
            var target = json.Choose(new[] { "on", "at" });
            _on = target == "on" ? json.Text("on") : null;
            var look = target == "at" ? json.Point("at") : float2.zero;
            _from = Aim(json.Child("from"), look, json.NumberOrNull("lift") ?? 0f, null);
            _to = json.Has("to") ? Aim(json.Child("to"), look, _from.Lift, _from) : _from;
            _seconds = json.Has("to") ? json.Number("seconds") : 0f;
            _fov = json.NumberOrNull("fov");
            if (json.Has("to") && _seconds <= 0f)
            {
                throw json.Problem($"needs 'seconds' above 0 to glide, got {_seconds}");
            }
        }

        public override string Kind => "glide";

        public override IEnumerable<string> Uses => _on == null ? base.Uses : new[] { _on };

        public static StagingStep Read(StagingObject step) => new GlideStep(step.Child("glide"));

        public override void Run(StagingRun run)
        {
            if (!run.Camera.TryGetComponent(out CinematicCamera cinematic))
            {
                cinematic = run.Camera.gameObject.AddComponent<CinematicCamera>();
            }

            if (_fov is { } fov)
            {
                run.Camera.fieldOfView = fov;
            }

            run.Follow = null;
            var group = _on == null ? null : run.FindGroup(_on);
            Func<float2?> track = group == null ? null : () => group.TryGetCentre(run.World, out var centre) ? centre : null;
            cinematic.Path = new GlidePath(_from, _to, UnityEngine.Time.time, _seconds, point => run.GroundPoint(point).y, track);
        }

        // An end aim takes what it leaves out from the start aim; a start aim needs its distance, pitch and yaw.
        private static CameraAim Aim(StagingObject json, float2 look, float lift, CameraAim? from)
        {
            float Read(string key, float? kept) => kept is { } value ? json.NumberOrNull(key) ?? value : json.Number(key);
            return new CameraAim(
                json.PointOrNull("at") ?? from?.Look ?? look,
                json.NumberOrNull("lift") ?? lift,
                Read("distance", from?.Distance),
                Read("pitch", from?.Pitch),
                Read("yaw", from?.Yaw));
        }
    }
}
