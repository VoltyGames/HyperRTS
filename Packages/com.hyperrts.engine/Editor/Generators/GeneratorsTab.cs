using System.Linq;
using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Stages;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// The Build page's Generators tab: every available <see cref="Generator"/> by group with its last run or item
    /// counts, whether it is out of date, and what it reads and writes; each runs alone or all the out of date ones.
    /// </summary>
    public sealed class GeneratorsTab : BuildTab
    {
        public override string Title => "Generators";

        public override int Order => 10;

        public override VisualElement Build()
        {
            var root = Layout().Instantiate();
            root.Require<ToolbarButton>("run-out-of-date").clicked += () => RunThenFill(GeneratorRunner.RunOutOfDate, root);
            Fill(root);
            return root;
        }

        // Filled again after a run, even a failed one, so the rows show its time and result.
        private void RunThenFill(Action run, VisualElement root)
        {
            try
            {
                run();
            }
            finally
            {
                Fill(root);
            }
        }

        private void Fill(VisualElement root)
        {
            var groups = root.Require<VisualElement>("groups");
            groups.Clear();
            var log = GeneratorRunLog.Load();
            var rowTemplate = Layout("Row");
            foreach (var group in GeneratorRunner.All().GroupBy(generator => generator.Group))
            {
                var section = new TitledSection { Title = group.Key };
                foreach (var generator in group)
                {
                    section.Add(Row(rowTemplate, generator, log, root));
                }

                groups.Add(section);
            }
        }

        private VisualElement Row(VisualTreeAsset template, Generator generator, GeneratorRunLog log,
            VisualElement root)
        {
            var row = template.CloneRoot();
            row.Require<Label>("name").text = generator.Name;
            row.Require<Label>("description").text = generator.Description;
            row.Require<Label>("sources").text = $"Reads: {generator.Sources}";
            row.Require<Label>("outputs").text = $"Writes: {generator.Outputs}";
            ShowLastRun(row, generator, log);
            row.Require<Button>("run").clicked += () => RunThenFill(() => GeneratorRunner.Run(generator), root);
            return row;
        }

        // A generator with items shows their counts; the Models page or the command lists each one.
        private static void ShowLastRun(VisualElement row, Generator generator, GeneratorRunLog log)
        {
            var label = row.Require<Label>("last-run");
            var items = generator.Items;
            if (items.Count == 0)
            {
                var last = log.Find(generator.Name, "");
                label.text = last == null ? "Not run in this project yet" : last.ToString();
                label.EnableInClassList("hrts-generators__last-run--failed", last is { Failed: true });
                row.Require<Label>("out-of-date").SetHidden(!generator.IsOutOfDate(items, log));
                return;
            }

            var states = items.Select(item => generator.State(item, log)).ToList();
            var failed = states.Count(state => state == StageState.Failed);
            var stale = states.Count(state => state != StageState.Done);
            label.text = $"{items.Count} items, {stale - failed} out of date, {failed} failed";
            label.EnableInClassList("hrts-generators__last-run--failed", failed > 0);
            row.Require<Label>("out-of-date").SetHidden(stale == 0);
        }
    }
}
