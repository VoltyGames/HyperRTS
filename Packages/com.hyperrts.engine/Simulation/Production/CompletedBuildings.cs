using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Snapshot of living, completed buildings: the only ones that satisfy prerequisites.</summary>
    public readonly struct CompletedBuildings
    {
        [ReadOnly] private readonly NativeList<EntityInfo> _infos;
        [ReadOnly] private readonly NativeList<Faction> _owners;

        /// <summary>Snapshots now, on the main thread, waiting for jobs that write the building components.</summary>
        public CompletedBuildings(EntityQuery query, AllocatorManager.AllocatorHandle allocator)
        {
            _infos = new NativeList<EntityInfo>(allocator);
            _owners = new NativeList<Faction>(allocator);
            _infos.CopyFrom(query.ToComponentDataArray<EntityInfo>(Allocator.Temp));
            _owners.CopyFrom(query.ToComponentDataArray<Faction>(Allocator.Temp));
        }

        /// <summary>
        /// Snapshots in jobs after <paramref name="dependsOn"/>, without a main-thread sync; read it only in jobs that
        /// depend on <paramref name="gathered"/>.
        /// </summary>
        public CompletedBuildings(EntityQuery query, AllocatorManager.AllocatorHandle allocator, JobHandle dependsOn,
            out JobHandle gathered)
        {
            _infos = query.ToComponentDataListAsync<EntityInfo>(allocator, dependsOn, out var infos);
            _owners = query.ToComponentDataListAsync<Faction>(allocator, dependsOn, out var owners);
            gathered = JobHandle.CombineDependencies(infos, owners);
        }

        public static EntityQueryBuilder Query(Allocator allocator) =>
            new EntityQueryBuilder(allocator)
                .WithAll<BuildingTag, EntityInfo, Faction>()
                .WithNone<ConstructionProgress, Dead>();

        /// <summary>Only snapshots <paramref name="completed"/> when something is required.</summary>
        public static bool MeetsPrerequisites(DynamicBuffer<Prerequisite> required, byte faction, EntityQuery completed) =>
            required.IsEmpty || new CompletedBuildings(completed, Allocator.Temp).MeetsPrerequisites(required, faction);

        /// <summary>True when <paramref name="faction"/> owns a completed building of every required type.</summary>
        public bool MeetsPrerequisites(DynamicBuffer<Prerequisite> required, byte faction)
        {
            foreach (var prerequisite in required)
            {
                if (!Owns(faction, prerequisite.TypeId))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when <paramref name="faction"/> owns a completed building of type <paramref name="typeId"/>.</summary>
        public bool Owns(byte faction, int typeId)
        {
            for (var i = 0; i < _infos.Length; i++)
            {
                if (_infos[i].TypeId == typeId && _owners[i].Value == faction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
