using System.Collections.Generic;
using HyperRTS.Editor.Effects.Preview;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>The Effects page lists a rig under the second field of its FX name, else under its folder.</summary>
    public class EffectRigEntryTests
    {
        [TestCase("Assets/Game/Prefabs/Effects/FX_Smoke_Heavy_01.prefab", "Smoke")]
        [TestCase("Assets/Game/Prefabs/Effects/FX_Fire.prefab", "Fire")]
        [TestCase("Assets/Game/Prefabs/Effects/Sparks/Welding.prefab", "Sparks")]
        [TestCase("Assets/Game/Prefabs/Effects/Embers_01.prefab", "Effects")]
        public void KindIsTheSecondFxFieldOrTheFolder(string path, string kind) =>
            Assert.That(new EffectRigEntry(path, new List<string>()).Kind, Is.EqualTo(kind));
    }
}
