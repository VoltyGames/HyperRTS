using HyperRTS.Network.Players;
using NUnit.Framework;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Which human slot a join gets: token owners reclaim theirs, strangers only take never-claimed slots.</summary>
    public class SlotClaimTests
    {
        private const ulong Mine = 0xA1;
        private const ulong Stranger = 0xB2;

        [Test]
        public void DroppedOwner_GetsItsSlotBack()
        {
            var slots = new[] { Held(1, 3, 0xC3), Dropped(2, Mine) };
            Assert.AreEqual(1, SlotClaim.Choose(slots, 1, Mine));
        }

        [Test]
        public void Stranger_CannotTakeADroppedSlot()
        {
            var slots = new[] { Held(1, 3, 0xC3), Dropped(2, Mine) };
            Assert.AreEqual(-1, SlotClaim.Choose(slots, 2, Stranger));
        }

        [Test]
        public void Owner_ReplacesAStaleConnection()
        {
            var slots = new[] { Held(1, 3, 0xC3), Held(2, 4, Mine) };
            var index = SlotClaim.Choose(slots, 0, Mine);
            Assert.AreEqual(1, index);
            Assert.AreNotEqual(0, slots[index].NetworkId, "the stale connection still holds it, so it is disconnected");
        }

        [Test]
        public void NewJoin_TakesTheWantedNeverClaimedSlot_ThenTheFirst()
        {
            var slots = new[] { Dropped(1, 0xC3), Open(2), Open(3) };
            Assert.AreEqual(2, SlotClaim.Choose(slots, 3, Stranger));
            Assert.AreEqual(1, SlotClaim.Choose(slots, 1, Stranger));
        }

        [Test]
        public void AnonymousSlot_IsFreeAgainOnceDropped()
        {
            var slots = new[] { Dropped(1, 0) };
            Assert.AreEqual(0, SlotClaim.Choose(slots, 0, Stranger));
        }

        [TestCase((byte)0, true, TestName = "ObserverRejoinsAsObserver")]
        [TestCase((byte)2, false, TestName = "PlayerRejoinsForItsSlot")]
        public void Rejoining_KeepsTheGrantedSeat(byte granted, bool observe)
        {
            var rejoin = JoinPreference.Rejoining(new LocalFaction { Value = granted });
            Assert.AreEqual(granted, rejoin.Faction);
            Assert.AreEqual(observe, rejoin.Observe);
        }

        private static SlotClaim.Slot Open(byte faction) => new() { Faction = faction };

        private static SlotClaim.Slot Dropped(byte faction, ulong token) => new() { Faction = faction, Token = token };

        private static SlotClaim.Slot Held(byte faction, int networkId, ulong token) =>
            new() { Faction = faction, NetworkId = networkId, Token = token };
    }
}
