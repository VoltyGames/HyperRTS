using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Adds the components every player entity carries.</summary>
    public static class PlayerSetup
    {
        /// <summary>Returns the player's empty stockpile.</summary>
        public static DynamicBuffer<ResourceStock> Add<TSink>(ref TSink sink, byte faction, in FixedString32Bytes name,
            float4 color, int populationCap) where TSink : struct, IComponentSink
        {
            sink.Add(new Player { Faction = faction, Name = name, Color = color });
            sink.Add(new Population { Cap = populationCap });
            sink.Add<PowerGrid>();
            sink.Add<Defeated>();
            sink.SetEnabled<Defeated>(false);
            sink.AddBuffer<PlayerCommand>();
            sink.AddBuffer<PlayerCommandSubject>();
            sink.AddBuffer<ResearchedUpgrade>();
            return sink.AddBuffer<ResourceStock>();
        }
    }
}
