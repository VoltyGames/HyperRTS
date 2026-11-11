using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Veterancy;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// The type handles the overlays read units through. Chunk reads skip the job sync EntityManager.GetComponentData
    /// does, so the constructor completes writers first.
    /// </summary>
    internal struct UnitHandles
    {
        public readonly EntityTypeHandle Entities;
        public ComponentTypeHandle<Owner> Owners;
        public ComponentTypeHandle<Health> Healths;
        public ComponentTypeHandle<LocalToWorld> Worlds;
        public ComponentTypeHandle<NavAgent> Agents;
        public ComponentTypeHandle<NavObstacle> Obstacles;
        public ComponentTypeHandle<Experience> Experience;
        public BufferTypeHandle<SquadMember> Members;
        public ComponentTypeHandle<Suppression> Suppression;
        public ComponentTypeHandle<Retreating> Retreating;
        private ComponentTypeHandle<Selected> _selected;
        private ComponentTypeHandle<Inside> _inside;

        public UnitHandles(EntityManager entityManager)
        {
            Entities = entityManager.GetEntityTypeHandle();
            Owners = Read<Owner>(entityManager);
            Healths = Read<Health>(entityManager);
            Worlds = Read<LocalToWorld>(entityManager);
            _selected = Read<Selected>(entityManager);
            _inside = Read<Inside>(entityManager);
            Agents = Read<NavAgent>(entityManager);
            Obstacles = Read<NavObstacle>(entityManager);
            Experience = Read<Experience>(entityManager);
            Suppression = Read<Suppression>(entityManager);
            Retreating = Read<Retreating>(entityManager);
            entityManager.CompleteDependencyBeforeRO<SquadMember>();
            Members = entityManager.GetBufferTypeHandle<SquadMember>(true);
        }

        public bool IsSelected(in ArchetypeChunk chunk, int index) =>
            chunk.Has(ref _selected) && chunk.IsComponentEnabled(ref _selected, index);

        // The query's Exclude<Inside> keeps whole chunks; an enabled bit has to be checked per entity.
        public bool IsInside(in ArchetypeChunk chunk, int index) =>
            chunk.Has(ref _inside) && chunk.IsComponentEnabled(ref _inside, index);

        private static ComponentTypeHandle<T> Read<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            entityManager.CompleteDependencyBeforeRO<T>();
            return entityManager.GetComponentTypeHandle<T>(true);
        }
    }
}
