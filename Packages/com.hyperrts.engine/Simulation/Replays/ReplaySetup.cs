using HyperRTS.Simulation.Match;
using Unity.Collections;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// The <see cref="MatchSetup"/> that rebuilds a recorded match's players (names, colours, teams, sides, AI, closed
    /// slots) on its map, so playback shows them as they were rather than as the map baked them.
    /// </summary>
    public static class ReplaySetup
    {
        /// <summary>The viewer sees through the first human player; replays reveal the whole map anyway.</summary>
        public static MatchSetup ToMatchSetup(Replay replay)
        {
            var setup = MatchSetup.Create();
            setup.Seed = replay.Seed;
            setup.Weather.CopyFromTruncated(replay.Weather ?? "");
            var last = 0;
            foreach (var player in replay.Players)
            {
                last = player.Faction > last ? player.Faction : last;
            }

            var viewer = FirstHuman(replay);
            for (var faction = 1; faction <= last; faction++)
            {
                setup.Slots.Add(Slot(replay, (byte)faction, viewer));
            }

            return setup;
        }

        private static SlotSetup Slot(Replay replay, byte faction, int viewer)
        {
            for (var i = 0; i < replay.Players.Count; i++)
            {
                var player = replay.Players[i];
                if (player.Faction != faction)
                {
                    continue;
                }

                var name = new FixedString32Bytes();
                name.CopyFromTruncated(player.Name ?? "");
                return new SlotSetup
                {
                    Open = true,
                    Control = PlaybackControl(player, i == viewer),
                    Difficulty = player.Difficulty,
                    Team = player.Team,
                    Color = player.Color,
                    Name = name,
                    Side = player.Side,
                };
            }

            return new SlotSetup { Open = false };
        }

        private static PlayerControl PlaybackControl(in ReplayPlayerInfo player, bool isViewer)
        {
            if (player.Control == PlayerControl.AI)
            {
                return PlayerControl.AI;
            }

            return isViewer ? PlayerControl.LocalHuman : PlayerControl.Remote;
        }

        /// <summary>Index of the first human player, or -1 when only bots played.</summary>
        private static int FirstHuman(Replay replay) =>
            replay.Players.FindIndex(player => player.Control != PlayerControl.AI);
    }
}
