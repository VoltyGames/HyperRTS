using System.Collections.Generic;
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
    /// Stamps each built <see cref="Trench"/> the local player sees into the scar map, where
    /// <see cref="CraterTerrainSystem"/> dents the terrain from it.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(TrampleStampSystem))]
    public partial class TrenchStampSystem : SystemBase
    {
        // Past the footprint's ends so neighbouring segments overlap and the ditch has no gaps.
        private const float EndOverlap = 0.1f;

        private readonly HashSet<Entity> _dug = new();

        protected override void OnUpdate()
        {
            var stamps = TrampleStampSystem.Find(World);
            if (stamps == null || !TrampleStampSystem.MapsDrawn)
            {
                return;
            }

            if (stamps.Reset)
            {
                _dug.Clear();
            }

            foreach (var (trench, obstacle, place, entity) in SystemAPI
                         .Query<RefRO<Trench>, RefRO<NavObstacle>, RefRO<LocalToWorld>>()
                         .WithNone<ConstructionProgress, FogHidden>().WithEntityAccess())
            {
                if (!_dug.Add(entity))
                {
                    continue;
                }

                var area = TurnedRect.Create(place.ValueRO.Value, place.ValueRO.Position.xz, obstacle.ValueRO.Size);
                stamps.AddTrench(area.Center, area.Right, new float2(area.Half.y, area.Half.x + EndOverlap), trench.ValueRO.Depth);
            }
        }
    }
}
