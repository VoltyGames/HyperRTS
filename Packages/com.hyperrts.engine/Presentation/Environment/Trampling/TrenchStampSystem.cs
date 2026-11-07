using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Environment.Trampling
{
    /// <summary>
    /// Stamps each built <see cref="Trench"/> the local player sees into the scar map once, where
    /// <see cref="CraterTerrainSystem"/> dents the terrain from it. A trench never seen built leaves no dent, and the
    /// scar outlives the building. Runs again for every trench after the maps reset.
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
            if (stamps == null || !TrampleStampSystem.MapsOn)
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

                var along = math.normalizesafe(place.ValueRO.Right.xz, new float2(1f, 0f));
                var half = obstacle.ValueRO.Size * 0.5f;
                stamps.AddTrench(place.ValueRO.Position.xz, along, new float2(half.y, half.x + EndOverlap), trench.ValueRO.Depth);
            }
        }
    }
}
