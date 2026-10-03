using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Tech-tree rules shared by the production commands and the HUD's button state.</summary>
    public static class ProductionRules
    {
        /// <summary>Living, completed buildings: the only ones that satisfy prerequisites.</summary>
        public static EntityQueryBuilder CompletedBuildings(Allocator allocator) =>
            new EntityQueryBuilder(allocator)
                .WithAll<BuildingTag, EntityInfo, Faction>()
                .WithNone<ConstructionProgress, Dead>();

        /// <summary>
        /// True when <paramref name="faction"/> owns a completed building of every required type.
        /// <paramref name="infos"/> and <paramref name="owners"/> come from <see cref="CompletedBuildings"/>.
        /// </summary>
        public static bool PrerequisitesMet(DynamicBuffer<Prerequisite> required, byte faction,
            NativeArray<EntityInfo> infos, NativeArray<Faction> owners)
        {
            foreach (var prerequisite in required)
            {
                if (!OwnsType(prerequisite.TypeId, faction, infos, owners))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool OwnsType(int typeId, byte faction, NativeArray<EntityInfo> infos, NativeArray<Faction> owners)
        {
            for (var i = 0; i < infos.Length; i++)
            {
                if (infos[i].TypeId == typeId && owners[i].Value == faction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
