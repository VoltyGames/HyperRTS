using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Cues.Effects;
using System.Runtime.CompilerServices;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Ground.Stamps
{
    /// <summary>
    /// Stamps the <see cref="DeathStain"/> of each death cue the local player sees on the ground into the remains map,
    /// such as fuel and wet mud under a destroyed vehicle. Deaths in the air leave nothing.
    /// </summary>
    // After TrampleStampSystem, which clears the stamps these join.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(TrampleStampSystem))]
    public partial class DeathStainSystem : SystemBase
    {
        private TypeCatalog<StrongBox<DeathStain>> _catalog;
        private CueVisibility _visibility;

        protected override void OnCreate()
        {
            _catalog = new TypeCatalog<StrongBox<DeathStain>>(SystemAPI.QueryBuilder()
                .WithAll<EntityInfo, DeathStain>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                (entityManager, source) => new StrongBox<DeathStain>(entityManager.GetComponentData<DeathStain>(source)));
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<CueQueue>();
            RequireForUpdate<PrefabRegistry>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (!TrampleStampSystem.MapsDrawn || cues.IsEmpty)
            {
                return;
            }

            var stamps = TrampleStampSystem.Find(World);
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            _visibility.Begin();
            foreach (var cue in cues)
            {
                if (cue.Slot != CueSlot.Death)
                {
                    continue;
                }

                var onGround = cue.Position.y - terrain.Height(cue.Position.xz) <= TrampleStampSystem.MaxGroundGap;
                if (!onGround || !_visibility.IsSeen(cue))
                {
                    continue;
                }

                var stain = _catalog.Find(EntityManager, registry, cue.TypeId);
                if (stain != null)
                {
                    stamps.AddScar(cue.Position.xz, stain.Value.Scar, stain.Value.Radius);
                }
            }
        }
    }
}
