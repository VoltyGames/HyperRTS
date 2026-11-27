using System;
using UnityEngine.InputSystem;

namespace HyperRTS.Presentation.HUD.Commands;

/// <summary>One command card button, the spec it came from and where the card put it.</summary>
public sealed class CommandEntry(CommandSpec spec, CommandButton button, Action invoke)
{
    public CommandSpec Spec { get; } = spec;

    public CommandButton Button { get; } = button;

    /// <summary>What the button and its hotkey do.</summary>
    public Action Invoke { get; } = invoke;

    /// <summary>The page the card shows it on: the spec's, unless that page was full.</summary>
    public int Page { get; set; } = spec.Page;

    /// <summary>The grid position the card gave it on <see cref="Page"/>.</summary>
    public int Slot { get; set; } = spec.Slot;

    public Key Hotkey => Spec.Hotkey;
}
