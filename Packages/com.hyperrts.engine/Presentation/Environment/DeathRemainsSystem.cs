using System.Runtime.CompilerServices;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Stamps the <see cref="DeathRemains"/> of each death cue the local player sees on the ground into the remains map,
    /// such as fuel and wet mud under a destroyed vehicle. Deaths in the air leave nothing.
    /// </summary>
    // After InteractionStampSystem, which clears the stamps these join.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InteractionStampSystem))]
    public partial class DeathRemainsSystem : SystemBase
    {
        private TypeCatalog<StrongBox<DeathRemains>> _catalog;
        private CueVisibility _visibility;
        private InteractionStampSystem _stampSystem;

        protected override void OnCreate()
        {
            _catalog = new TypeCatalog<StrongBox<DeathRemains>>(SystemAPI.QueryBuilder()
                .WithAll<EntityInfo, DeathRemains>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                (entityManager, source) => new StrongBox<DeathRemains>(entityManager.GetComponentData<DeathRemains>(source)));
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<CueQueue>();
            RequireForUpdate<PrefabRegistry>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (InteractionMap.Quality == InteractionQuality.Off || cues.IsEmpty)
            {
                return;
            }

            var stamps = (_stampSystem ??= World.GetExistingSystemManaged<InteractionStampSystem>()).Stamps;
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            _visibility.Begin();
            foreach (var cue in cues)
            {
                var onGround = cue.Position.y - terrain.Height(cue.Position.xz) <= InteractionMath.MaxGroundGap;
                if (cue.Slot != CueSlot.Death || !onGround || !_visibility.IsSeen(cue))
                {
                    continue;
                }

                var remains = _catalog.Find(EntityManager, registry, cue.TypeId);
                if (remains != null)
                {
                    stamps.AddRemains(cue.Position.xz, remains.Value.Radius, remains.Value.Amounts);
                }
            }
        }
    }
}
