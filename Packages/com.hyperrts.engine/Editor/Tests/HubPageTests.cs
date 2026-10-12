using System;
using System.Linq;
using HyperRTS.Editor.Hub;
using NUnit.Framework;
using UnityEditor;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The Tools window's sidebar: every page, engine or game, needs its own title and position.</summary>
    public class HubPageTests
    {
        private static HubPage[] Pages() => TypeCache.GetTypesDerivedFrom<HubPage>()
            .Where(type => !type.IsAbstract)
            .Select(type => (HubPage)Activator.CreateInstance(type))
            .ToArray();

        [Test]
        public void PagesExist() => Assert.That(Pages(), Is.Not.Empty);

        [Test]
        public void TitlesAreUnique()
        {
            var duplicates = Pages().GroupBy(page => page.Title).Where(group => group.Count() > 1).Select(group => group.Key);
            Assert.That(duplicates, Is.Empty, "Hub pages sharing a Title");
        }

        [Test]
        public void OrdersAreUnique()
        {
            var duplicates = Pages().GroupBy(page => page.Order).Where(group => group.Count() > 1)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(page => page.GetType().Name))}");
            Assert.That(duplicates, Is.Empty, "Hub pages sharing an Order");
        }
    }
}
