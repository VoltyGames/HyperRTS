using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Cues.Voice
{
    /// <summary>
    /// What a squad's bark system saw last frame, so it can tell a change from a state. Client only: never a ghost
    /// field, and added when the squad first appears, which stays silent.
    /// </summary>
    public struct SquadBarkState : IComponentData
    {
        public bool Known;
        public SuppressionLevel Level;
        public bool Retreating;
        public bool Covered;
        public bool Digging;
        public int Living;

        /// <summary>Game time before which this squad barks no more.</summary>
        public double QuietUntil;

        /// <summary>
        /// Records the squad's state and returns the cue slot of the strongest change since last time, or
        /// <see cref="CueSlot.None"/>. The first look only records. Pinned beats suppressed, then retreat, reinforced,
        /// digging in and taking cover.
        /// </summary>
        public CueSlot Observe(in SquadRules rules, in Suppression suppression, DynamicBuffer<SquadMember> members)
        {
            var living = 0;
            var covered = 0;
            var digging = false;
            foreach (var member in members)
            {
                if (!member.Alive)
                {
                    continue;
                }

                living++;
                covered += member.Cover != CoverKind.None ? 1 : 0;
                digging |= member.Action == MemberAction.Dig;
            }

            var now = new SquadBarkState
            {
                Known = true, Level = rules.Level(suppression.Value), Retreating = suppression.Retreating,
                Covered = covered * 2 > living, Digging = digging, Living = living, QuietUntil = QuietUntil,
            };
            var slot = Known ? Strongest(now) : CueSlot.None;
            this = now;
            return slot;
        }

        private readonly CueSlot Strongest(in SquadBarkState now)
        {
            if (now.Level > Level)
            {
                return now.Level == SuppressionLevel.Pinned ? CueSlot.Pinned : CueSlot.Suppressed;
            }

            if (now.Retreating && !Retreating)
            {
                return CueSlot.Retreat;
            }

            if (now.Living > Living)
            {
                return CueSlot.Reinforced;
            }

            if (now.Digging && !Digging)
            {
                return CueSlot.DigIn;
            }

            return now.Covered && !Covered ? CueSlot.TakeCover : CueSlot.None;
        }
    }
}
