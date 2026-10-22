using System;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One command card button and where and how it is reached.</summary>
    public sealed class CommandEntry
    {
        public CommandEntry(CommandButton button, Action invoke)
        {
            Button = button;
            Invoke = invoke;
        }

        /// <summary>A plain caption button (stances, unit actions).</summary>
        public CommandEntry(string caption, Action invoke) : this(new CommandButton().Bind(caption, invoke), invoke)
        {
            Button.AddToClassList("hud-command--text");
        }

        public CommandButton Button { get; }

        /// <summary>What the button and its hotkey do.</summary>
        public Action Invoke { get; }

        /// <summary>Grid position, lowest first; entries without one follow in the order their sources added them.</summary>
        public int Slot { get; set; } = -1;

        /// <summary>0 for the first page, 1 for the second behind a More card (a builder's advanced options).</summary>
        public int Page { get; set; }

        /// <summary>A letter or digit that presses the button while it is enabled; '\0' for none.</summary>
        public char Hotkey { get; set; }
    }
}
