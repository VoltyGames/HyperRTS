using System;
using UnityEngine.InputSystem;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>One command card button, the spec it came from and where the card put it.</summary>
    public sealed class CommandEntry
    {
        public CommandEntry(CommandSpec spec, CommandButton button, Action invoke)
        {
            Spec = spec;
            Button = button;
            Invoke = invoke;
            Page = spec.Page;
            Slot = spec.Slot;
        }

        public CommandSpec Spec { get; }

        public CommandButton Button { get; }

        /// <summary>What the button and its hotkey do.</summary>
        public Action Invoke { get; }

        /// <summary>The page the card shows it on: the spec's, unless that page was full.</summary>
        public int Page { get; set; }

        /// <summary>The grid position the card gave it on <see cref="Page"/>.</summary>
        public int Slot { get; set; }

        public Key Hotkey => Spec.Hotkey;
    }
}
