using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.ImpactMarks;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Environment.Ground.Stamps
{
    /// <summary>
    /// Stamps the <see cref="WornGround"/> of each finished building the local player sees into the stain map once, as
    /// wet mud round its footprint for the rest of the match.
    /// </summary>
    // After ImpactMarkSystem, which flags a replay seek back, so the cleared maps get the mud again that frame.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(TrampleStampSystem))]
    [UpdateAfter(typeof(ImpactMarkSystem))]
    public partial class WornGroundStampSystem : SystemBase
    {
        private readonly HashSet<Entity> _stamped = new();

        protected override void OnUpdate()
        {
            var stamps = TrampleStampSystem.Find(World);
            if (stamps == null || !TrampleStampSystem.MapsDrawn)
            {
                return;
            }

            if (stamps.Reset)
            {
                _stamped.Clear();
            }

            // A trench is dug earth already.
            foreach (var (worn, obstacle, place, entity) in SystemAPI
                         .Query<RefRO<WornGround>, RefRO<NavObstacle>, RefRO<LocalToWorld>>()
                         .WithNone<ConstructionProgress, FogHidden, Trench>().WithEntityAccess())
            {
                if (!_stamped.Add(entity))
                {
                    continue;
                }

                var position = place.ValueRO.Position.xz;
                var footprint = TurnedRect.Create(place.ValueRO.Value, position, obstacle.ValueRO.Size);
                stamps.AddWornGround(footprint, worn.ValueRO.Width, worn.ValueRO.Strength, math.hash(position));
            }
        }
    }
}
