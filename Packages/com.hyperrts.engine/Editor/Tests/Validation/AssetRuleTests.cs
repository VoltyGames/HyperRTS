using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Simulation.Cues.Sounds;
using UnityEngine.InputSystem;
using System.Linq;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>Asset rules check assets a test makes in memory, and the set loads each type once per pass.</summary>
    public class AssetRuleTests : TemplateFixture
    {
        [Test]
        public void CueWithoutClips_IsAnError_AndOneWithAClipPasses()
        {
            var silent = Track(ScriptableObject.CreateInstance<SoundCue>());
            var playing = Track(ScriptableObject.CreateInstance<SoundCue>());
            playing.clips = new[] { Track(AudioClip.Create("Shot", 441, 1, 44100, false)) };
            var issues = new ValidationIssues();

            new SoundCueRule().Check(AssetSet.From(silent, playing), issues);

            Assert.AreEqual(silent, issues.Single().Context);
            Assert.AreEqual(MessageType.Error, issues.Single().Severity);
        }

        [Test]
        public void CommandSpecs_ShareNoHotkeyOnAPage_AndTakeNoBoundKey()
        {
            var first = Track(CommandSpec.Create(CommandTarget.Order, Key.B, labelKey: "command.sell"));
            var twin = Track(CommandSpec.Create(CommandTarget.Order, Key.B, labelKey: "command.return"));
            var otherPage = Track(CommandSpec.Create(CommandTarget.Order, Key.B, page: 1, labelKey: "command.unload"));
            var bound = Track(CommandSpec.Create(CommandTarget.Order, Key.A, labelKey: "command.deploy"));
            var issues = new ValidationIssues();

            new CommandSpecRule().Check(AssetSet.From(first, twin, otherPage, bound), issues);

            CollectionAssert.AreEquivalent(new UnityEngine.Object[] { twin, bound }, issues.Select(issue => issue.Context).ToArray());
        }

        [Test]
        public void AssetSet_ReturnsTheSameListForTheSameRequest()
        {
            var assets = AssetSet.From(Track(ScriptableObject.CreateInstance<SoundCue>()));

            Assert.AreSame(assets.All<SoundCue>(), assets.All<SoundCue>());
            Assert.AreEqual(1, assets.All<SoundCue>().Count);
        }
    }
}
