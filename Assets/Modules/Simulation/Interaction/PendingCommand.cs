using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.Interaction
{
    /// <summary>A targeted command (e.g. attack-move) armed by a hotkey or HUD button; the next world click issues it.</summary>
    public struct PendingCommand : IComponentData
    {
        public CommandType Type;
    }
}
