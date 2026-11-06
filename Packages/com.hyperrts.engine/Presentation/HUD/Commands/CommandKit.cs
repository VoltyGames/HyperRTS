using System;
using HyperRTS.Presentation.HUD;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>The specs and the button template that <see cref="ICommandSource"/>s build their entries from.</summary>
    public sealed class CommandKit
    {
        public CommandKit(VisualTreeAsset buttonParts, CommandCatalogue catalogue)
        {
            ButtonParts = buttonParts;
            Catalogue = catalogue;
        }

        public VisualTreeAsset ButtonParts { get; }

        public CommandCatalogue Catalogue { get; }

        /// <summary>
        /// A button for <paramref name="spec"/>. The spec's label and icon win over <paramref name="name"/> and
        /// <paramref name="icon"/>; an order shows as a caption unless it has an icon.
        /// </summary>
        public CommandEntry Create(CommandSpec spec, string name, Texture2D icon, Action invoke)
        {
            var label = spec.LabelKey.Length > 0 ? HUDText.Text(spec.LabelKey) : name;
            var picture = spec.Icon != null ? spec.Icon : icon;
            var picked = spec.Target == CommandTarget.Order && picture == null ? CommandLook.Text : CommandLook.Picture;
            var button = new CommandButton(ButtonParts, picked, label, picture, spec.HotkeyText, invoke);
            return new CommandEntry(spec, button, invoke);
        }
    }
}
