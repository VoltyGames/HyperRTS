using System.Linq;
using HyperRTS.Editor.PlayMode.Launch;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.PlayMode
{
    /// <summary>How the launcher deals slots and teams for a mode and map.</summary>
    public class LaunchChoiceTests
    {
        [Test]
        public void DealPutsYouFirstWithBotsOnTheOtherTeamAndClosesTheRest()
        {
            var choice = new LaunchChoice { mode = MatchMode.TwoVsTwo };

            choice.Deal(6);

            Assert.That(choice.slots.Select(slot => slot.kind), Is.EqualTo(new[]
            {
                LaunchSlotKind.Human, LaunchSlotKind.Bot, LaunchSlotKind.Bot, LaunchSlotKind.Bot,
                LaunchSlotKind.Closed, LaunchSlotKind.Closed,
            }));
            Assert.That(choice.slots.Select(slot => slot.team), Is.EqualTo(new[] { 1, 1, 2, 2, 0, 0 }));
            Assert.That(choice.Problem(6), Is.Empty);
        }

        [Test]
        public void DealKeepsASlotsSideWhenTheModeChanges()
        {
            var choice = new LaunchChoice { mode = MatchMode.OneVsOne };
            choice.Deal(4);
            choice.slots[1].side = 3;

            choice.mode = MatchMode.FreeForAll;
            choice.Deal(4);

            Assert.That(choice.slots[1].side, Is.EqualTo(3));
            Assert.That(choice.slots.Select(slot => slot.team), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void ProblemRejectsOneTeamAndTwoHumansOffline()
        {
            var choice = new LaunchChoice { mode = MatchMode.OneVsOne };
            choice.Deal(2);

            choice.slots[1].team = 1;
            Assert.That(choice.Problem(2), Is.Not.Empty);

            choice.slots[1].team = 2;
            choice.slots[1].kind = LaunchSlotKind.Human;
            Assert.That(choice.Problem(2), Is.Not.Empty);

            choice.network = LaunchNetwork.Host;
            Assert.That(choice.Problem(2), Is.Empty);
        }

        [Test]
        public void LabelNamesTheBotDifficultyOrMixedBots()
        {
            var choice = new LaunchChoice { mode = MatchMode.TwoVsTwo };
            choice.Deal(4);
            Assert.That(choice.Label("Chornozem"), Is.EqualTo("Chornozem 2v2 vs Normal"));

            choice.slots[2].difficulty = HyperRTS.Simulation.Match.AIDifficulty.Hard;
            Assert.That(choice.Label("Chornozem"), Is.EqualTo("Chornozem 2v2 vs mixed bots"));
        }
    }
}
