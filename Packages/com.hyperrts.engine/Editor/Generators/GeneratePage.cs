using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
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
            var root = new VisualElement();
            EditorAssets.AddStyles(root);
            var runAll = new Button(GeneratorRunner.RunAll) { text = "Run all" };
            runAll.AddToClassList("hrts-generate__run-all");
            root.Add(runAll);

            foreach (var group in GeneratorRunner.All().GroupBy(generator => generator.Group))
            {
                root.Add(Section(group.Key, group));
            }

            return root;
        }

        private static VisualElement Section(string title, IGrouping<string, Generator> generators)
        {
            var section = new VisualElement();
            section.AddToClassList("hrts-section");
            var heading = new Label(title);
            heading.AddToClassList("hrts-section__title");
            section.Add(heading);
            foreach (var generator in generators)
            {
                section.Add(Row(generator));
            }

            return section;
        }

        private static VisualElement Row(Generator generator)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-generate__row");

            var text = new VisualElement();
            text.AddToClassList("hrts-row__grow");
            var name = new Label(generator.Name);
            name.AddToClassList("hrts-generate__name");
            text.Add(name);
            var description = new Label(generator.Description);
            description.AddToClassList("hrts-generate__description");
            text.Add(description);
            row.Add(text);

            var run = new Button(() => GeneratorRunner.Run(generator)) { text = "Run" };
            run.AddToClassList("hrts-generate__run");
            row.Add(run);
            return row;
        }
    }
}
