using System;
using System.Linq;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Tests.Tools
{
    /// <summary>The map assembly's Tools window pages and UXML controls build.</summary>
    public class MapToolsWindowTests
    {
        [Test]
        public void MapPagesBuild()
        {
            var pages = TypeCache.GetTypesDerivedFrom<HubPage>()
                .Where(type => !type.IsAbstract && type.Assembly == typeof(MapsPage).Assembly)
                .Select(type => (HubPage)Activator.CreateInstance(type))
                .ToArray();

            Assert.That(pages, Is.Not.Empty);
            foreach (var page in pages)
            {
                Assert.That(page.Build(), Is.Not.Null, page.Title);
            }
        }

        [TestCaseSource(nameof(Controls))]
        public void ControlBuilds(Type control) =>
            Assert.DoesNotThrow(() => Activator.CreateInstance(control), control.Name);

        private static Type[] Controls() => TypeCache.GetTypesWithAttribute<UxmlElementAttribute>()
            .Where(type => type.Assembly == typeof(MapsPage).Assembly)
            .ToArray();
    }
}
