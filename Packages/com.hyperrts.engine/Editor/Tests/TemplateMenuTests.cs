using System;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Templates;
using NUnit.Framework;
using UnityEditor;

namespace HyperRTS.Editor.Tests
{
    /// <summary>GameObject ▸ HyperRTS and Assets ▸ Create ▸ HyperRTS ▸ Prefabs list the same templates.</summary>
    public class TemplateMenuTests : TemplateFixture
    {
        private const string ObjectMenu = "GameObject/HyperRTS/";
        private const string PrefabMenu = "Assets/Create/HyperRTS/Prefabs/";

        [Test]
        public void BothMenusListEveryTemplate()
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
