using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Adds the components every player entity carries.</summary>
    public static class PlayerSetup
    {
        /// <summary>Returns the player's empty stockpile.</summary>
        public static DynamicBuffer<ResourceStock> Add<TWriter>(ref TWriter writer, byte faction,
            in FixedString32Bytes name, float4 color, int populationCap) where TWriter : struct, IEntityWriter
        {
            writer.Add(new Player { Faction = faction, Name = name, Color = color });
            writer.Add<PlayerSide>();
            writer.Add(new Population { Cap = populationCap });
            writer.Add<PowerGrid>();
            writer.Add<Defeated>();
            writer.SetEnabled<Defeated>(false);
            writer.AddBuffer<PlayerCommand>();
            writer.AddBuffer<PlayerCommandSubject>();
            writer.AddBuffer<ResearchedUpgrade>();
            return writer.AddBuffer<ResourceStock>();
        }
    }
}
