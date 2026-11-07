using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Strikes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.MatchStart
{
    /// <summary>Adds the components every player entity carries.</summary>
    public static class PlayerSetup
    {
        /// <summary>Returns the player's empty stockpile.</summary>
        public static DynamicBuffer<ResourceStock> Add(EntityWriter writer, byte faction,
            in FixedString32Bytes name, float4 color, int populationCap)
        {
            writer.Add(new Player { Faction = faction, Name = name, Color = color });
            writer.Add<PlayerSide>();
            writer.Add<PlayerStats>();
            writer.Add(new Population { Cap = populationCap });
            writer.Add<PowerGrid>();
            writer.Add<StrategicCharge>();
            writer.Add<Defeated>();
            writer.SetEnabled<Defeated>(false);
            writer.AddBuffer<PlayerCommand>();
            writer.AddBuffer<PlayerCommandSubject>();
            writer.AddBuffer<CompletedResearch>();
            // Player-wide stats such as Income; games add their own.
            writer.AddBuffer<StatModifier>();
            return writer.AddBuffer<ResourceStock>();
        }
    }
}
