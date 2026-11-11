using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>What <see cref="CommandSpec.Create"/> sets beyond the target.</summary>
    public sealed class CommandSpecSettings
    {
        public Key Hotkey = Key.None;
        public int Page;
        public int Slot;
        public string LabelKey = "";
        public Texture2D Icon;
        public CommandType Order = CommandType.None;
        public int Argument;
        public string Ability = "";
        public ProducibleAuthoring Prefab;
        public ProducibleAuthoring Card;
    }
}
