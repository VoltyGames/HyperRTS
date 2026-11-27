using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering.LitTeam;

/// <summary>
/// HyperRTS/LitTeam's sink radius on a render entity: the half length of the vehicle or wreck it is part of, which
/// drops into a crater only as far as the shallowest ground that far round it.
/// </summary>
[MaterialProperty("_SinkRadius")]
public struct SinkRadius : IComponentData
{
    public float Value;
}
