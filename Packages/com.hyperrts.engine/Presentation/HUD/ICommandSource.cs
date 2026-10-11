using System.Collections.Generic;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A group of command card buttons (building, abilities, a game's own orders). The card asks every source to
    /// build when the selection changes and to refresh every frame.
    /// </summary>
    public interface ICommandSource
    {
        /// <summary>Adds entries for <paramref name="owned"/>, the local player's part of the selection.</summary>
        void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries);

        /// <summary>Updates availability, highlights and countdowns of the entries this source built.</summary>
        void Refresh(HUDContext context);
    }
}
