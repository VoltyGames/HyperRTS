using System;
using System.Linq;
using HyperRTS.Editor.Hub;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Every engine UXML layout loads and has the elements its script looks up by name.</summary>
    public class EditorLayoutTests
    {
        private static Type[] Controls() => TypeCache.GetTypesWithAttribute<UxmlElementAttribute>()
            .Where(type => type.Assembly == typeof(HubPage).Assembly)
            .ToArray();

        [Test]
        public void ControlsExist() => Assert.That(Controls(), Is.Not.Empty);

        [TestCaseSource(nameof(Controls))]
        public void ControlBuilds(Type control) =>
            Assert.DoesNotThrow(() => Activator.CreateInstance(control), control.Name);

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
    }
}
