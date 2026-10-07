using HyperRTS.Simulation.Match;
using Unity.Collections;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// The <see cref="MatchSetup"/> that rebuilds a recorded match's players (names, colours, teams, sides, closed
    /// slots) on its map, so playback shows them as they were rather than as the map baked them.
    /// </summary>
    public static class ReplaySetup
    {
        /// <summary>The viewer sees through the first recorded player; replays reveal the whole map anyway.</summary>
        public static MatchSetup ToMatchSetup(Replay replay)
        {
            var setup = MatchSetup.Create();
            var last = 0;
            foreach (var player in replay.Players)
            {
                last = player.Faction > last ? player.Faction : last;
            }

            for (var faction = 1; faction <= last; faction++)
            {
                setup.Slots.Add(Slot(replay, (byte)faction));
            }

            return setup;
        }

        private static SlotSetup Slot(Replay replay, byte faction)
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
                    Control = i == 0 ? PlayerControl.LocalHuman : PlayerControl.Remote,
                    Team = player.Team,
                    Color = player.Color,
                    Name = name,
                    Side = player.Side,
                };
            }

            return new SlotSetup { Open = false };
        }
    }
}
