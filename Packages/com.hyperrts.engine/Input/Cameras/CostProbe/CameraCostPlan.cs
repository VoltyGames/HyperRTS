using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Input.Cameras.CostProbe;

/// <summary>The points file of <see cref="CameraCostProbe"/>: where to look, from which heights, and for how long.</summary>
[Serializable]
public sealed class CameraCostPlan
{
    public List<CameraCostPoint> points = new();

    [Tooltip("Camera heights above the ground to measure at each point.")]
    public List<float> heights = new();

    [Tooltip("Frames sampled at each point and height.")]
    public int sampleFrames;

    [Tooltip("Frames to wait after each move before sampling.")]
    public int settleFrames;

    /// <exception cref="FormatException">The file is not a plan, or leaves something out.</exception>
    public static CameraCostPlan Parse(string json)
    {
        CameraCostPlan plan;
        try
        {
            plan = JsonUtility.FromJson<CameraCostPlan>(json);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException($"The points file is not valid JSON: {exception.Message}", exception);
        }

        plan.Validate();
        return plan;
    }

    private void Validate()
    {
        if (points.Count == 0)
        {
            throw new FormatException("The points file has no \"points\".");
        }

        if (heights.Count == 0)
        {
            throw new FormatException("The points file has no \"heights\".");
        }

        if (sampleFrames <= 0 || settleFrames < 0)
        {
            throw new FormatException("The points file needs a positive \"sampleFrames\" and a \"settleFrames\" of 0 or more.");
        }

        var unnamed = points.FindIndex(point => string.IsNullOrEmpty(point.name));
        if (unnamed >= 0)
        {
            throw new FormatException($"Point {unnamed} of the points file has no \"name\".");
        }
    }
}
