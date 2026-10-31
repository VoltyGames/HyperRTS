using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Scenery
{
    /// <summary>Server state for scenery replication. A team hears of a change only once it sees the spot.</summary>
    public struct SceneryTracker : IDisposable
    {
        /// <summary>One slot per fog team. Team 0 is for observers and sees everything.</summary>
        public const int Teams = FactionRelations.MaxTeams;

        private NativeHashMap<ulong, int> _slots;
        private NativeList<SceneryRecord> _records;

        /// <summary>Last <see cref="SceneryChange.Code"/> each team saw, at <c>slot * Teams + team</c>.</summary>
        private NativeList<ushort> _known;

        /// <summary>Last broken mask each team saw, at the same index as <see cref="_known"/>.</summary>
        private NativeList<ulong> _knownBroken;

        /// <summary>Slots some team hasn't caught up with.</summary>
        private NativeList<int> _stale;

        public SceneryTracker(Allocator allocator)
        {
            _slots = new NativeHashMap<ulong, int>(1024, allocator);
            _records = new NativeList<SceneryRecord>(1024, allocator);
            _known = new NativeList<ushort>(1024 * Teams, allocator);
            _knownBroken = new NativeList<ulong>(1024 * Teams, allocator);
            _stale = new NativeList<int>(64, allocator);
        }

        public readonly int Count => _records.Length;

        public void Dispose()
        {
            _slots.Dispose();
            _records.Dispose();
            _known.Dispose();
            _knownBroken.Dispose();
            _stale.Dispose();
        }

        /// <summary>False when the id is already tracked.</summary>
        public bool Register(ulong id, float3 position)
        {
            if (!_slots.TryAdd(id, _records.Length))
            {
                return false;
            }

            _records.Add(new SceneryRecord { Id = id, Position = position, State = SceneryChange.Intact });
            for (var team = 0; team < Teams; team++)
            {
                _known.Add(SceneryChange.IntactCode);
                _knownBroken.Add(0);
            }

            return true;
        }

        /// <summary>Records a state change. Teams in <paramref name="teams"/> that saw another state get a sighting.</summary>
        public bool Set(ulong id, byte state, ulong broken, byte fall, ushort teams, uint frame)
        {
            if (!_slots.TryGetValue(id, out var slot) ||
                (_records[slot].State == state && _records[slot].Broken == broken))
            {
                return false;
            }

            var record = _records[slot];
            record.State = state;
            record.Broken = broken;
            record.Fall = fall;
            Mark(slot, record, teams, frame);
            return true;
        }

        /// <summary>Records the piece, or its remains once destroyed, removed with nothing left.</summary>
        public bool Clear(ulong id, ushort teams, uint frame)
        {
            if (!_slots.TryGetValue(id, out var slot) || _records[slot].Cleared)
            {
                return false;
            }

            var record = _records[slot];
            record.State = SceneryChange.Destroyed;
            record.Cleared = true;
            Mark(slot, record, teams, frame);
            return true;
        }

        /// <summary>With fog off every team sees everything. Changes made this <paramref name="frame"/> count as witnessed.</summary>
        public void Reveal(in FogOfWar fog, bool fogOn, uint frame, NativeList<ScenerySighting> sightings)
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

                    _known[slot * Teams + team] = record.Change.Code;
                    _knownBroken[slot * Teams + team] = record.Broken;
                    record.Stale &= (ushort)~(1 << team);
                    sightings.Add(new ScenerySighting { Team = (byte)team, Witnessed = record.Changed == frame, Change = record.Change });
                }

                if (record.Stale == 0)
                {
                    record.Listed = false;
                    _stale.RemoveAtSwapBack(i);
                }

                _records[slot] = record;
            }
        }

        /// <summary>What <paramref name="team"/> knows that differs from the loaded map, for a joining client.</summary>
        public readonly void Known(int team, NativeList<SceneryChange> changes)
        {
            for (var slot = 0; slot < _records.Length; slot++)
            {
                var known = _known[slot * Teams + team];
                var broken = _knownBroken[slot * Teams + team];
                if (known != SceneryChange.IntactCode || broken != 0)
                {
                    var record = _records[slot];
                    changes.Add(SceneryChange.FromCode(record.Id, known, broken, record.Fall));
                }
            }
        }

        private void Mark(int slot, SceneryRecord record, ushort teams, uint frame)
        {
            record.Changed = frame;
            record.Stale = StaleTeams(slot, record.Change.Code, record.Broken, teams);
            if (record.Stale != 0 && !record.Listed)
            {
                record.Listed = true;
                _stale.Add(slot);
            }

            _records[slot] = record;
        }

        private readonly ushort StaleTeams(int slot, ushort code, ulong broken, ushort teams)
        {
            var stale = 0;
            for (var team = 0; team < Teams; team++)
            {
                var bit = 1 << team;
                var index = slot * Teams + team;
                if ((teams & bit) != 0 && (_known[index] != code || _knownBroken[index] != broken))
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

            // Scenery past the fog grid's edge uses the edge cell.
            var cell = math.clamp(fog.WorldToCell(position), 0, fog.Size - 1);
            return FogOfWar.HasTeam(fog.Visible[fog.Index(cell)], (byte)team);
        }
    }
}
