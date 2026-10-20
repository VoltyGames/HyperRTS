using System;
using System.Linq;
using System.Reflection;
using HyperRTS.Presentation.Common;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Every presentation and input behaviour stays off without a graphics device through one base.</summary>
    public class PresentationBehaviourTests
    {
        private const BindingFlags Declared =
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type[] Behaviours() =>
            new[] { typeof(PresentationGate).Assembly, Assembly.Load("HyperRTS.Input") }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type) && type != typeof(PresentationBehaviour))
            .ToArray();

        [Test]
        public void EveryBehaviour_DerivesFromPresentationBehaviour()
        {
            var strays = Behaviours().Where(type => !typeof(PresentationBehaviour).IsAssignableFrom(type));
            CollectionAssert.IsEmpty(strays.Select(type => type.FullName));
        }

        // A subclass's own Awake or OnDestroy would replace the base's and skip the graphics check.
        [Test]
        public void NoBehaviour_DeclaresItsOwnAwakeOrOnDestroy()
        {
            var strays = Behaviours().Where(type =>
                type.GetMethod("Awake", Declared) != null || type.GetMethod("OnDestroy", Declared) != null);
            CollectionAssert.IsEmpty(strays.Select(type => type.FullName));
        }
    }
}
