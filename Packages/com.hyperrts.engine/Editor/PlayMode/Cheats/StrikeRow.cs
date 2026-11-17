using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>A row of the Strikes cheats: a strike's title and either Build at view or its status and Fire at view.</summary>
    internal sealed class StrikeRow
    {
        private readonly Label _status;

        public StrikeRow(StrikeEntry entry)
        {
            Entry = entry;
            Root = EditorAssets.Layout(typeof(StrikeRow)).CloneTree();
            Root.Require<Label>("title").text = entry.Title;
            _status = Root.Require<Label>("status");
            Build = Root.Require<Button>("build");
            Fire = Root.Require<Button>("fire");
        }

        public StrikeEntry Entry { get; }

        public VisualElement Root { get; }

        public Button Build { get; }

        public Button Fire { get; }

        /// <summary>The player owns none: offer to build one.</summary>
        public void ShowMissing()
        {
            _status.text = "";
            Build.SetHidden(false);
            Fire.SetHidden(true);
        }

        /// <summary>The player owns one: its status, or why it can't fire.</summary>
        public void ShowOwned(string status, string blocker)
        {
            _status.text = blocker ?? status;
            Build.SetHidden(true);
            Fire.SetHidden(false);
            Fire.SetEnabled(blocker == null);
        }
    }
}
