using System;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Which human slot a join request gets: the slot its token owns (even while a stale connection holds it), else
    /// a never-claimed slot (the wanted one first), else none, which makes the sender an observer.
    /// </summary>
    public static class SlotClaim
    {
        /// <summary>A human slot as the server sees it.</summary>
        public struct Slot
        {
            public byte Faction;

            /// <summary>0 while no connection holds the slot.</summary>
            public int NetworkId;

            /// <summary>The token of the last client that held the slot; 0 if none or it sent none.</summary>
            public ulong Token;
        }

        /// <summary>The index of the slot to bind, or -1 for none.</summary>
        public static int Choose(ReadOnlySpan<Slot> slots, byte wanted, ulong token)
        {
            var owned = Owned(slots, token);
            if (owned >= 0)
            {
                return owned;
            }

            var free = -1;
            for (var i = 0; i < slots.Length; i++)
            {
                if (!IsFree(slots[i]))
                {
                    continue;
                }

                if (slots[i].Faction == wanted)
                {
                    return i;
                }

                if (free < 0)
                {
                    free = i;
                }
            }

            return free;
        }

        private static int Owned(ReadOnlySpan<Slot> slots, ulong token)
        {
            if (token == 0)
            {
                return -1;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].Token == token)
                {
                    return i;
                }
            }

            return -1;
        }

        // A dropped slot keeps its token, so it waits for its owner instead of going to whoever joins next.
        private static bool IsFree(Slot slot) => slot.NetworkId == 0 && slot.Token == 0;
    }
}
