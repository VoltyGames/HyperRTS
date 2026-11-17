using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Points the match camera at a group's centre or a map point; it may keep following the group.</summary>
    internal sealed class CameraStep : StagingStep
    {
        private readonly string _on;
        private readonly float2 _point;
        private readonly float _height;
        private readonly float? _yaw;
        private readonly bool _follow;

        private CameraStep(StagingObject json)
        {
            var target = json.Choose(new[] { "on", "at" });
            _on = target == "on" ? json.Text("on") : null;
            _point = target == "at" ? json.Point("at") : default;
            _height = json.Number("height");
            _yaw = json.NumberOrNull("yaw");
            _follow = json.Has("follow") && json.Flag("follow");
            if (_follow && _on == null)
            {
                throw json.Problem("can follow only a group: use 'on', not 'at'");
            }
        }

        public override string Kind => "camera";

        public override IEnumerable<string> Uses => _on == null ? base.Uses : new[] { _on };

        public static StagingStep Read(StagingObject step) => new CameraStep(step.Child("camera"));

        public override void Run(StagingRun run)
        {
            var centre = _on == null ? _point : run.GroupCentre(_on);
            run.Controller.LookAt(new Vector3(centre.x, 0f, centre.y), _height, _yaw);
            run.Follow = _follow ? _on : null;
        }
    }
}
