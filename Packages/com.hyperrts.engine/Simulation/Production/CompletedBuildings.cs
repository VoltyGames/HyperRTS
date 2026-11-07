using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Snapshot of living, completed buildings: the only ones that satisfy prerequisites.</summary>
    public readonly struct CompletedBuildings
    {
        [ReadOnly] private readonly NativeList<EntityInfo> _infos;
        [ReadOnly] private readonly NativeList<Owner> _owners;

        /// <summary>Snapshots now, on the main thread, waiting for jobs that write the building components.</summary>
        public CompletedBuildings(EntityQuery query, AllocatorManager.AllocatorHandle allocator)
        {
            _infos = new NativeList<EntityInfo>(allocator);
            _owners = new NativeList<Owner>(allocator);
            _infos.CopyFrom(query.ToComponentDataArray<EntityInfo>(Allocator.Temp));
            _owners.CopyFrom(query.ToComponentDataArray<Owner>(Allocator.Temp));
        }

        /// <summary>
        /// Snapshots in jobs after <paramref name="dependsOn"/>, without a main-thread sync; read it only in jobs that
        /// depend on <paramref name="gathered"/>.
        /// </summary>
        public CompletedBuildings(EntityQuery query, AllocatorManager.AllocatorHandle allocator, JobHandle dependsOn,
            out JobHandle gathered)
        {
            _infos = query.ToComponentDataListAsync<EntityInfo>(allocator, dependsOn, out var infos);
            _owners = query.ToComponentDataListAsync<Owner>(allocator, dependsOn, out var owners);
            gathered = JobHandle.CombineDependencies(infos, owners);
        }

        public static EntityQueryBuilder Query(Allocator allocator) =>
            new EntityQueryBuilder(allocator)
                .WithAll<BuildingTag, EntityInfo, Owner>()
                .WithNone<ConstructionProgress, Dead>();

        /// <summary>
        /// True when <paramref name="owner"/> has, for every required type, a completed building of it or finished
        /// research of it in <paramref name="research"/>.
        /// </summary>
        public bool MeetsPrerequisites(DynamicBuffer<Prerequisite> required, byte owner,
            DynamicBuffer<CompletedResearch> research)
        {
            foreach (var prerequisite in required)
            {
                if (!IsMet(owner, prerequisite.TypeId, research))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when <paramref name="owner"/> has a completed building or finished research of it.</summary>
        public bool IsMet(byte owner, int typeId, DynamicBuffer<CompletedResearch> research) =>
            Owns(owner, typeId) || ResearchRules.IsDone(research, typeId);

        /// <summary>True when <paramref name="owner"/> owns a completed building of type <paramref name="typeId"/>.</summary>
        public bool Owns(byte owner, int typeId)
        {
            for (var i = 0; i < _infos.Length; i++)
            {
                if (_infos[i].TypeId == typeId && _owners[i].Value == owner)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
