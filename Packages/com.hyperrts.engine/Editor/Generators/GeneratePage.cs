using System;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// Tools ▸ Generate: every available <see cref="Generator"/> by group with what it reads and writes and its last
    /// run, each runnable alone or all in order.
    /// </summary>
    public sealed class GeneratePage : HubPage
    {
        public override string Title => "Generate";

        public override int Order => 90;

        public override VisualElement Build()
        {
            var root = EditorAssets.Layout(typeof(GeneratePage)).Instantiate();
            root.Require<Button>("run-all").clicked += () => RunThenFill(GeneratorRunner.RunAll, root);
            Fill(root);
            return root;
        }

        // Filled again after a run, even a failed one, so the rows show its time and result.
        private static void RunThenFill(Action run, VisualElement root)
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

        private static void Fill(VisualElement root)
        {
            var groups = root.Require<VisualElement>("groups");
            groups.Clear();
            var log = GeneratorRunLog.Load();
            var groupTemplate = EditorAssets.Layout(typeof(GeneratePage), "Group");
            var rowTemplate = EditorAssets.Layout(typeof(GeneratePage), "Row");
            foreach (var group in GeneratorRunner.All().GroupBy(generator => generator.Group))
            {
                var section = groupTemplate.CloneRoot();
                section.Require<Label>("title").text = group.Key;
                foreach (var generator in group)
                {
                    section.Add(Row(rowTemplate, generator, log, root));
                }

                groups.Add(section);
            }
        }

        private static VisualElement Row(VisualTreeAsset template, Generator generator, GeneratorRunLog log,
            VisualElement root)
        {
            var row = template.CloneRoot();
            row.Require<Label>("name").text = generator.Name;
            row.Require<Label>("description").text = generator.Description;
            row.Require<Label>("sources").text = $"Reads: {generator.Sources}";
            row.Require<Label>("outputs").text = $"Writes: {generator.Outputs}";
            var last = log.Find(generator.Name);
            var lastRun = row.Require<Label>("last-run");
            lastRun.text = last == null ? "Not run in this project yet" : last.ToString();
            lastRun.EnableInClassList("hrts-generate__last-run--failed", last is { Failed: true });
            row.Require<Button>("run").clicked += () => RunThenFill(() => GeneratorRunner.Run(generator), root);
            return row;
        }
    }
}
