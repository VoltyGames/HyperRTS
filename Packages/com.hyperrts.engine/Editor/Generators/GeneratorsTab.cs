using System;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// The Build page's Generators tab: every available <see cref="Generator"/> by group with its last run, whether
    /// it is out of date, and what it reads and writes; each runs alone or all the out of date ones in order.
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
            row.Require<Label>("out-of-date").SetHidden(!generator.IsOutOfDate(log));
            var last = log.Find(generator.Name);
            var lastRun = row.Require<Label>("last-run");
            lastRun.text = last == null ? "Not run in this project yet" : last.ToString();
            lastRun.EnableInClassList("hrts-generators__last-run--failed", last is { Failed: true });
            row.Require<Button>("run").clicked += () => RunThenFill(() => GeneratorRunner.Run(generator), root);
            return row;
        }
    }
}
