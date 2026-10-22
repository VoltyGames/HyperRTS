using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>Tools ▸ Generate: every available <see cref="Generator"/> by group, each runnable alone or all in order.</summary>
    public sealed class GeneratePage : HubPage
    {
        public override string Title => "Generate";

        public override int Order => 90;

        public override VisualElement Build()
        {
            var root = EditorAssets.Layout(typeof(GeneratePage)).Instantiate();
            root.Require<Button>("run-all").clicked += GeneratorRunner.RunAll;
            var groups = root.Require<VisualElement>("groups");
            var groupTemplate = EditorAssets.Layout(typeof(GeneratePage), "Group");
            var rowTemplate = EditorAssets.Layout(typeof(GeneratePage), "Row");
            foreach (var group in GeneratorRunner.All().GroupBy(generator => generator.Group))
            {
                var section = groupTemplate.CloneRoot();
                section.Require<Label>("title").text = group.Key;
                foreach (var generator in group)
                {
                    section.Add(Row(rowTemplate, generator));
                }

                groups.Add(section);
            }

            return root;
        }

        private static VisualElement Row(VisualTreeAsset template, Generator generator)
        {
            var row = template.CloneRoot();
            row.Require<Label>("name").text = generator.Name;
            row.Require<Label>("description").text = generator.Description;
            row.Require<Button>("run").clicked += () => GeneratorRunner.Run(generator);
            return row;
        }
    }
}
