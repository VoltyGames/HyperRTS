using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Stages
{
    /// <summary>A stage's row on a tool page: its title, its state, and Log and Run buttons.</summary>
    public sealed class StageRow
    {
        public StageRow(string title, StageState state)
        {
            Root = EditorAssets.Layout(typeof(StageRow)).CloneRoot();
            Root.Require<Label>("title").text = title;
            ShowState(Root.Require<Label>("state"), state);
            Log = Root.Require<Button>("log");
            Run = Root.Require<Button>("run");
        }

        public VisualElement Root { get; }

        public Button Log { get; }

        public Button Run { get; }

        public static string Describe(StageState state) => state switch
        {
            StageState.Done => "Done",
            StageState.OutOfDate => "Out of date",
            StageState.Missing => "Missing",
            _ => "Failed",
        };

        /// <summary>Writes <paramref name="state"/> on <paramref name="label"/>, tinted by the <c>hrts-stage__state</c> classes.</summary>
        public static void ShowState(Label label, StageState state)
        {
            label.text = Describe(state);
            label.EnableInClassList("hrts-stage__state--stale", state is StageState.OutOfDate or StageState.Missing);
            label.EnableInClassList("hrts-stage__state--failed", state == StageState.Failed);
        }
    }
}
