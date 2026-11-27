using System;
using System.Collections.Generic;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Points the match camera at a group's centre or a map point, taking it back from a cinematic view; it may keep following the group.</summary>
internal sealed class CameraStep : StagingStep
{
    public string on;
    public float[] at;
    public float height;
    public float? yaw;
    public bool? follow;

    private float2 _point;

    public override IEnumerable<string> Uses => on == null ? base.Uses : new[] { on };

    public override void Check()
    {
        var target = OneOf(("on", on != null), ("at", at != null));
        _point = target == "at" ? Point(at, "at") : default;
        if (follow == true && on == null)
        {
            throw new InvalidOperationException("can follow only a group: use 'on', not 'at'");
        }
    }

    public override void Run(StagingRun run)
    {
        if (run.Camera.TryGetComponent(out CinematicCamera cinematic))
        {
            cinematic.Path = null;
        }

        var centre = on == null ? _point : run.GroupCentre(on);
        run.Controller.LookAt(new Vector3(centre.x, 0f, centre.y), height, yaw);
        run.Follow = follow == true ? on : null;
    }
}
