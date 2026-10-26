using System;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Generators;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Templates;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>The Tools window, its UXML controls, the generator list and the template menus.</summary>
    public class ToolsWindowTests : TemplateFixture
    {
        private const string ObjectMenu = "GameObject/HyperRTS/";
        private const string PrefabMenu = "Assets/Create/HyperRTS/Prefabs/";

        [Test]
        public void HubPagesHaveUniqueTitlesAndOrders()
        {
            var pages = TypeCache.GetTypesDerivedFrom<HubPage>()
                .Where(type => !type.IsAbstract)
                .Select(type => (HubPage)Activator.CreateInstance(type))
                .ToArray();

            Assert.That(pages, Is.Not.Empty);
            Assert.That(pages.Select(page => page.Title), Is.Unique, "Hub pages sharing a Title");
            Assert.That(pages.Select(page => page.Order), Is.Unique, "Hub pages sharing an Order");
        }

        [Test]
        public void EnginePagesBuild()
        {
            var pages = TypeCache.GetTypesDerivedFrom<HubPage>()
                .Where(type => !type.IsAbstract && type.Assembly == typeof(HubPage).Assembly)
                .Select(type => (HubPage)Activator.CreateInstance(type));
            foreach (var page in pages)
            {
                Assert.That(page.Build(), Is.Not.Null, page.Title);
                page.Release();
            }
        }

        [Test]
        public void ControlsExist() => Assert.That(Controls(), Is.Not.Empty);

        [TestCaseSource(nameof(Controls))]
        public void ControlBuilds(Type control) =>
            Assert.DoesNotThrow(() => Activator.CreateInstance(control), control.Name);

        [Test]
        public void GeneratorsAreUniqueInRunOrderAndDescribed()
        {
            var generators = GeneratorRunner.All().ToList();

            Assert.That(generators.Select(generator => generator.Name), Is.Unique);
            Assert.That(generators.Select(generator => generator.Order), Is.Ordered);
            foreach (var generator in generators)
            {
                Assert.That(generator.Group, Is.Not.Empty, generator.Name);
                Assert.That(generator.Description, Is.Not.Empty, generator.Name);
            }
        }

        [Test]
        public void BothTemplateMenusListEveryTemplate()
        {
            var registry = TemplateRegistry.All.Select(template => template.Path).OrderBy(path => path).ToArray();

            // Scene setup items (rig, Match) sit at the top level; every template is in a submenu.
            var objects = MenuPaths("HyperRTSObjectMenu", ObjectMenu).Where(path => path.Contains('/'));
            CollectionAssert.AreEqual(registry, objects.OrderBy(path => path).ToArray());
            CollectionAssert.AreEqual(registry, MenuPaths("PrefabTemplateMenu", PrefabMenu).OrderBy(path => path).ToArray());
        }

        [Test]
        public void TemplatesAreNamedAfterTheirLabel()
        {
            foreach (var template in TemplateRegistry.All)
            {
                Assert.AreEqual(template.Label, Track(template.Create()).name, template.Path);
            }
        }

        private static Type[] Controls() => TypeCache.GetTypesWithAttribute<UxmlElementAttribute>()
            .Where(type => type.Assembly == typeof(HubPage).Assembly)
            .ToArray();

        private static string[] MenuPaths(string menuType, string prefix)
        {
            var type = typeof(TemplateRegistry).Assembly.GetType("HyperRTS.Editor.Templates." + menuType)
                       ?? throw new InvalidOperationException($"Menu class {menuType} is missing.");
            return type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .SelectMany(method => method.GetCustomAttributes<MenuItem>())
                .Select(item => item.menuItem)
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .Select(path => path.Substring(prefix.Length))
                .ToArray();
        }
    }
}
