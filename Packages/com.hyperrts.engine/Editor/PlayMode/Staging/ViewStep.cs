using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// Takes the camera off the player for a cinematic move: from one aim to another over some seconds, or a held aim,
    /// at a group (followed as it moves) or a map point. With <c>sun</c>, yaws count from the way the sunlight falls, so
    /// yaw 0 keeps the sun behind the camera on any map. A later camera step hands the camera back.
    /// </summary>
    internal sealed class ViewStep : StagingStep
    {
        private readonly string _on;
        private readonly CameraAim _from;
        private readonly CameraAim _to;
        private readonly float _seconds;
        private readonly float? _fov;
        private readonly bool _sun;

        private ViewStep(StagingObject json)
        {
            var target = json.Choose(new[] { "on", "at" });
            _on = target == "on" ? json.Text("on") : null;
            var look = target == "at" ? json.Point("at") : float2.zero;
            _from = Aim(json.Child("from"), look, json.NumberOrNull("lift") ?? 0f, null);
            var moves = json.Has("to");
            _to = moves ? Aim(json.Child("to"), look, _from.Lift, _from) : _from;
            _seconds = moves ? json.Number("seconds") : 0f;
            _fov = json.NumberOrNull("fov");
            _sun = json.Has("sun") && json.Flag("sun");
            if (moves && _seconds <= 0f)
            {
                throw json.Problem($"needs 'seconds' above 0 to move, got {_seconds}");
            }
        }

        public override string Kind => "view";

        public override IEnumerable<string> Uses => _on == null ? base.Uses : new[] { _on };

        public static StagingStep Read(StagingObject step) => new ViewStep(step.Child("view"));

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
            var turn = _sun ? SunHeading() : 0f;
            cinematic.Path = new ViewPath(_from.Turned(turn), _to.Turned(turn), Time.time, _seconds, point => run.GroundPoint(point).y, track);
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
