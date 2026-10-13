using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Scenery
{
    /// <summary>
    /// What the server tracks to replicate scenery: each piece's state and the state each team last saw of it. A
    /// team gets news of a change only once it sees the spot, so fog hides destruction until someone looks.
    /// </summary>
    public struct SceneryTracker : IDisposable
    {
        /// <summary>One slot per fog team; team 0, which observers view as, sees everything.</summary>
        public const int Teams = FactionRelations.MaxTeams;

        private NativeHashMap<ulong, int> _slots;
        private NativeList<SceneryRecord> _records;

        /// <summary>Last state each team saw, at <c>slot * Teams + team</c>.</summary>
        private NativeList<byte> _known;

        /// <summary>Slots some team hasn't caught up with.</summary>
        private NativeList<int> _stale;

        public SceneryTracker(Allocator allocator)
        {
            _slots = new NativeHashMap<ulong, int>(1024, allocator);
            _records = new NativeList<SceneryRecord>(1024, allocator);
            _known = new NativeList<byte>(1024 * Teams, allocator);
            _stale = new NativeList<int>(64, allocator);
        }

        public readonly int Count => _records.Length;

        public void Dispose()
        {
            _slots.Dispose();
            _records.Dispose();
            _known.Dispose();
            _stale.Dispose();
        }

        /// <summary>Tracks a piece as the map loaded it, intact for every team; false when the id is already tracked.</summary>
        public bool Register(ulong id, float3 position)
        {
            if (!_slots.TryAdd(id, _records.Length))
            {
                return false;
            }

            _records.Add(new SceneryRecord { Id = id, Position = position, State = SceneryChange.Intact });
            for (var team = 0; team < Teams; team++)
            {
                _known.Add(SceneryChange.Intact);
            }

            return true;
        }

        /// <summary>
        /// Records the piece's state at <paramref name="frame"/>; each team in <paramref name="teams"/> (a bit each)
        /// that last saw another state has news. <paramref name="fall"/> goes with a destruction. Returns whether the
        /// state changed.
        /// </summary>
        public bool Set(ulong id, byte state, byte fall, ushort teams, uint frame)
        {
            if (!_slots.TryGetValue(id, out var slot) || _records[slot].State == state)
            {
                return false;
            }

            var record = _records[slot];
            record.State = state;
            record.Fall = fall;
            record.Changed = frame;
            record.Stale = StaleTeams(slot, state, teams);
            if (record.Stale != 0 && !record.Listed)
            {
                record.Listed = true;
                _stale.Add(slot);
            }

            _records[slot] = record;
            return true;
        }

        /// <summary>
        /// Hands each team the news it can see now into <paramref name="news"/>. With fog off or no fog grid every
        /// team sees everything; news of a change made this <paramref name="frame"/> was witnessed.
        /// </summary>
        public void Reveal(in FogOfWar fog, bool fogOn, uint frame, NativeList<SceneryNews> news)
        {
            var everything = !fogOn || !fog.IsCreated;
            for (var i = _stale.Length - 1; i >= 0; i--)
            {
                var slot = _stale[i];
                var record = _records[slot];
                for (var team = 0; team < Teams; team++)
                {
                    if (!IsStale(record, team) || !CanSee(fog, everything, record.Position, team))
                    {
                        continue;
                    }

                    _known[slot * Teams + team] = record.State;
                    record.Stale &= (ushort)~(1 << team);
                    news.Add(new SceneryNews { Team = (byte)team, Witnessed = record.Changed == frame, Change = record.Change });
                }

                if (record.Stale == 0)
                {
                    record.Listed = false;
                    _stale.RemoveAtSwapBack(i);
                }

                _records[slot] = record;
            }
        }

        /// <summary>What <paramref name="team"/> knows that differs from the map as loaded: a joining client's catch-up.</summary>
        public readonly void Known(int team, NativeList<SceneryChange> changes)
        {
            for (var slot = 0; slot < _records.Length; slot++)
            {
                var known = _known[slot * Teams + team];
                if (known != SceneryChange.Intact)
                {
                    var record = _records[slot];
                    changes.Add(new SceneryChange { Id = record.Id, State = known, Fall = record.Fall });
                }
            }
        }

        private readonly ushort StaleTeams(int slot, byte state, ushort teams)
        {
            var stale = 0;
            for (var team = 0; team < Teams; team++)
            {
                var bit = 1 << team;
                if ((teams & bit) != 0 && _known[slot * Teams + team] != state)
                {
                    stale |= bit;
                }
            }

            return (ushort)stale;
        }

        private static bool IsStale(in SceneryRecord record, int team) => (record.Stale & (1 << team)) != 0;

        private static bool CanSee(in FogOfWar fog, bool everything, float3 position, int team)
        {
            if (team == 0 || everything)
            {
                return true;
            }

            // Scenery past the edge of the fog grid shows what the edge cell does.
            var cell = math.clamp(fog.WorldToCell(position), 0, fog.Size - 1);
            return FogOfWar.HasTeam(fog.Visible[fog.Index(cell)], (byte)team);
        }
    }
}
