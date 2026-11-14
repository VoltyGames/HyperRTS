using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Marks the gone squads whose death cue the local player saw, so only they leave corpses.</summary>
    internal sealed class CorpseDeathCues
    {
        // Metres between a death cue and the squad it names: the ghost lags the server a little.
        private const float CueReach = 2f;

        private readonly EntityManager _entities;
        private readonly EntityQuery _ends;
        private CueVisibility _visibility;

        public CorpseDeathCues(ref SystemState state, EntityManager entities)
        {
            _entities = entities;
            _visibility = new CueVisibility(ref state);
            _ends = state.GetEntityQuery(ComponentType.ReadOnly<SquadEnd>());
        }

        public void Match(DynamicBuffer<CueEvent> cues, float now)
        {
            // A death the local player didn't see leaves nothing for them, as the figures it hid were hidden too.
            _visibility.Begin();
            using var squads = _ends.ToEntityArray(Allocator.Temp);
            using var ends = _ends.ToComponentDataArray<SquadEnd>(Allocator.Temp);
            foreach (var cue in cues)
            {
                if (cue.Slot == CueSlot.Death && _visibility.IsSeen(cue))
                {
                    MarkDied(cue, squads, ends, now);
                }
            }
        }

        // The nearest unmarked squad of the cue's type, one whose ghost is gone first: a remote client may get the
        // cue before the despawn, but a live squad beside the dead one is never taken for it.
        private void MarkDied(in CueEvent cue, NativeArray<Entity> squads, NativeArray<SquadEnd> ends, float now)
        {
            var nearest = -1;
            var nearestGone = false;
            var best = CueReach;
            for (var i = 0; i < squads.Length; i++)
            {
                var distance = math.distance(ends[i].Position.xz, cue.Position.xz);
                if (ends[i].Died || ends[i].TypeId != cue.TypeId || distance > CueReach)
                {
                    continue;
                }

                var gone = !_entities.HasComponent<SquadFigures>(squads[i]);
                if (Prefers(gone, distance, nearestGone, best))
                {
                    nearest = i;
                    nearestGone = gone;
                    best = distance;
                }
            }

            if (nearest < 0)
            {
                return;
            }

            var died = ends[nearest];
            died.Died = true;
            died.DiedAt = now;
            ends[nearest] = died;
            _entities.SetComponentData(squads[nearest], died);
        }

        private static bool Prefers(bool gone, float distance, bool bestGone, float best)
        {
            if (gone != bestGone)
            {
                return gone;
            }

            return distance <= best;
        }
    }
}
