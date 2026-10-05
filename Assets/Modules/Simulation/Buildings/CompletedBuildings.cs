using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Snapshot of living, completed buildings: the only ones that satisfy prerequisites.</summary>
    public readonly struct CompletedBuildings
    {
        private readonly NativeArray<EntityInfo> _infos;
        private readonly NativeArray<Faction> _owners;

        public CompletedBuildings(EntityQuery query, AllocatorManager.AllocatorHandle allocator)
        {
            _infos = query.ToComponentDataArray<EntityInfo>(allocator);
            _owners = query.ToComponentDataArray<Faction>(allocator);
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
