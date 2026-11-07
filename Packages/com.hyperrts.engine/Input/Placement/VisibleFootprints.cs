using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Input.Placement
{
    /// <summary>
    /// The building and obstacle footprints the local player can see, which a placement spot must clear by the map's
    /// building gap. Hidden ones are left to the server, so the ghost never gives them away.
    /// </summary>
    public struct VisibleFootprints
    {
        private EntityQuery _obstacles;

        public VisibleFootprints(ref SystemState state)
        {
            _obstacles = new EntityQueryBuilder(Allocator.Temp).WithAll<NavObstacle, LocalTransform>()
                .WithNone<Dead, FogHidden>().Build(ref state);
        }

        /// <summary>The seen footprints copied once, so a line of spots is checked against one read.</summary>
        public readonly NativeList<NavBox> Read() => NavObstacle.ReadBoxes(_obstacles);
    }
}
