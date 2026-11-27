using System;
using System.Collections.Generic;

namespace HyperRTS.Input.Cameras.CostProbe;

/// <summary>What <see cref="CameraCostProbe"/> writes: a result for each point and height, in the plan's order.</summary>
[Serializable]
public sealed class CameraCostReport
{
    public List<CameraCostResult> results = [];
}
