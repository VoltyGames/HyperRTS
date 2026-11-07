using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>A <see cref="PlayerCommand"/> with the player who issued it, as <see cref="CommandReader"/> yields it.</summary>
    public struct IssuedCommand
    {
        public Entity Player;
        public byte Owner;
        public PlayerCommand Command;
    }
}
