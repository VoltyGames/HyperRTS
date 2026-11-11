using System;
using System.Linq;
using HyperRTS.Editor.Maps.Designer.Window;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Tests.Tools
{
    /// <summary>The map assembly's UXML controls build.</summary>
    public class MapToolsWindowTests
    {
        [TestCaseSource(nameof(Controls))]
        public void ControlBuilds(Type control) =>
            Assert.DoesNotThrow(() => Activator.CreateInstance(control), control.Name);

        private static Type[] Controls() => TypeCache.GetTypesWithAttribute<UxmlElementAttribute>()
            .Where(type => type.Assembly == typeof(MapGallery).Assembly)
            .ToArray();
    }
}
