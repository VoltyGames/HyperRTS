using HyperRTS.Core;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Poses model parts (turrets, wheels, machine parts, falling trees) through their <see cref="LocalTransform"/>s on
    /// presented worlds before the transform systems build matrices, so the parts render the same frame as the unit.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented, SimulationWorlds.Presented)]
    // First in the transform group: after movement and replay, which run before it, and before any matrix is built.
    [UpdateInGroup(typeof(TransformSystemGroup), OrderFirst = true)]
    public partial class PoseSystemGroup : ComponentSystemGroup
    {
    }
}
