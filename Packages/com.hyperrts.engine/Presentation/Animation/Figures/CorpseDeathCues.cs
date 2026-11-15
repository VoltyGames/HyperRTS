using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Marks the gone squads whose death cue the local player saw, so only they leave corpses.</summary>
    internal sealed class CorpseDeathCues
    {
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
                    MarkDied(cue.SourceNumber, squads, ends, now);
                }
            }
        }

        // The squad's ghost may go before or after its cue arrives; either way its number finds it.
        private void MarkDied(int number, NativeArray<Entity> squads, NativeArray<SquadEnd> ends, float now)
        {
            for (var i = 0; i < squads.Length; i++)
            {
                if (ends[i].SpawnNumber != number)
                {
                    continue;
                }

                var died = ends[i];
                died.Died = true;
                died.DiedAt = now;
                ends[i] = died;
                _entities.SetComponentData(squads[i], died);
                return;
            }
        }
    }
}
