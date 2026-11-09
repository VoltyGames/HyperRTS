using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>What the launcher starts: remembered in the editor state and handed to <see cref="GameLauncher.Start"/>.</summary>
    [Serializable]
    public class LaunchChoice
    {
        public string map = "";
        public TeamLayout mode = TeamLayout.TwoVsTwo;
        public LaunchEntry entry;
        public LaunchNetwork network;
        public List<LaunchSlot> slots = new();

        /// <summary>The short name of a mode, such as "2v2".</summary>
        public static string ModeName(TeamLayout mode) => mode switch
        {
            TeamLayout.FreeForAll => "FFA",
            TeamLayout.OneVsOne => "1v1",
            TeamLayout.TwoVsTwo => "2v2",
            TeamLayout.ThreeVsThree => "3v3",
            TeamLayout.FourVsFour => "4v4",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        /// <summary>Slots per team, or 0 when every slot plays alone.</summary>
        private static int TeamSize(TeamLayout mode) => mode == TeamLayout.FreeForAll ? 0 : (int)mode;

        /// <summary>
        /// Fits the table to a map with <paramref name="mapSlots"/> slots and deals the teams for the mode: you in the first
        /// slot, bots after you and the rest closed. Sides and difficulties stay with their slot.
        /// </summary>
        public void Deal(int mapSlots)
        {
            while (slots.Count < mapSlots)
            {
                slots.Add(new LaunchSlot());
            }

            slots.RemoveRange(mapSlots, slots.Count - mapSlots);
            var size = TeamSize(mode);
            var alone = size == 0;
            var players = alone ? mapSlots : Math.Min(mapSlots, size * 2);
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i].kind = i switch
                {
                    0 => LaunchSlotKind.Human,
                    _ when i < players => LaunchSlotKind.Bot,
                    _ => LaunchSlotKind.Closed,
                };
                if (i >= players)
                {
                    slots[i].team = 0;
                }
                else
                {
                    slots[i].team = alone ? i + 1 : i / size + 1;
                }
            }
        }

        /// <summary>"Chornozem 2v2 vs Normal": the main toolbar's label.</summary>
        public string Label(string mapName)
        {
            var bots = slots.Where(slot => slot.kind == LaunchSlotKind.Bot).Select(slot => slot.difficulty).Distinct().ToList();
            var label = $"{mapName} {ModeName(mode)}";
            return bots.Count switch
            {
                0 => label,
                1 => $"{label} vs {bots[0]}",
                _ => $"{label} vs mixed bots",
            };
        }

        /// <summary>Why the table can't start on <paramref name="game"/>'s chosen map, or "" when it can.</summary>
        public string Problem(GameLauncher game) => game.TryFindMap(map, out var found) ? Problem(found.Slots) : "No map";

        /// <summary>Why the table can't start, or "" when it can.</summary>
        public string Problem(int mapSlots)
        {
            if (slots.Count != mapSlots)
            {
                return "The slots don't fit the map.";
            }

            var active = slots.Where(slot => slot.kind != LaunchSlotKind.Closed).ToList();
            var humans = active.Count(slot => slot.kind == LaunchSlotKind.Human);
            if (active.Count < 2)
            {
                return "A match needs at least two players.";
            }

            // A player without a team plays alone.
            var sides = active.Select((slot, index) => slot.team > 0 ? slot.team : -index - 1).Distinct().Count();
            if (sides < 2)
            {
                return "A match needs players on at least two teams.";
            }

            if (network == LaunchNetwork.Offline && humans != 1)
            {
                return "An offline match has exactly one human.";
            }

            if (network == LaunchNetwork.Host && humans == 0)
            {
                return "Hosting needs a human slot for yourself.";
            }

            if (network == LaunchNetwork.Dedicated && humans == 0)
            {
                return "A dedicated server needs a human slot for players to join.";
            }

            return "";
        }
    }
}
