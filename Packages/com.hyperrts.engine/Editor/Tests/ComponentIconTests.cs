using HyperRTS.Simulation.Common.Authoring;
using System.IO;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Icons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Every engine authoring component has its own icon, glyph and Add Component folder.</summary>
    public class ComponentIconTests
    {
        private static readonly string[] MenuFolders = { HyperRTSMenu.Entity, HyperRTSMenu.Look, HyperRTSMenu.Scene };

        private static System.Type[] Components() => TypeCache.GetTypesDerivedFrom<AuthoringBehaviour>()
            .Where(type => !type.IsAbstract && !type.IsGenericType && type.Namespace.StartsWith("HyperRTS.") && !type.Assembly.GetName().Name.EndsWith(".Tests"))
            .ToArray();

        [Test]
        public void EachComponentHasItsOwnIconAndGlyph()
        {
            var paths = Components().Select(type => (Type: type, Icon: type.GetCustomAttribute<IconAttribute>()?.path)).ToList();
            var problems = paths.Where(entry => entry.Icon == null).Select(entry => $"{entry.Type.Name} has no [Icon]").ToList();
            problems.AddRange(paths.Where(entry => entry.Icon != null && Path.GetFileNameWithoutExtension(entry.Icon) != entry.Type.Name)
                .Select(entry => $"{entry.Type.Name} uses the icon {entry.Icon}"));
            problems.AddRange(paths.Where(entry => entry.Icon != null && !File.Exists($"Packages/com.hyperrts.engine/Editor/Icons/Components/Source/{entry.Type.Name}.svg"))
                .Select(entry => $"{entry.Type.Name} has no glyph in Components/Source"));
            problems.AddRange(paths.Where(entry => entry.Icon != null && !ComponentIconGenerator.IsListed(entry.Type.Name)).Select(entry => $"{entry.Type.Name} is on no module tile"));
            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void MenuEntriesSitUnderEntityLookOrScene()
        {
            var stray = Components().Select(type => (Type: type, Menu: type.GetCustomAttribute<AddComponentMenu>()?.componentMenu))
                .Where(entry => !string.IsNullOrEmpty(entry.Menu) && !MenuFolders.Any(folder => entry.Menu.StartsWith(folder)))
                .Select(entry => $"{entry.Type.Name}: {entry.Menu}");
            CollectionAssert.IsEmpty(stray);
        }
    }
}
