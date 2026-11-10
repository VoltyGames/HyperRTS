using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class CorpseSystem
    {
        // Metres between a death cue and the squad it names: the ghost lags the server a little.
        private const float CueReach = 2f;

        // Seconds a gone squad waits for its death cue, which a remote client gets apart from the ghost's despawn.
        private const float CueWait = 0.5f;

        private void Scan()
        {
            _handovers.Clear();
            new ScanJob
            {
                Now = _now,
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Handovers = _handovers,
            }.Run();
        }

        private void MatchDeathCues()
        {
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true))
            {
                return;
            }

            // A death the local player didn't see leaves nothing for them, as the figures it hid were hidden too.
            _visibility.Begin();
            foreach (var cue in cues)
            {
                if (cue.Slot == CueSlot.Death && _visibility.IsSeen(cue))
                {
                    MarkDied(cue);
                }
            }
        }

        // The nearest squad of the cue's type, so another one dying beside it isn't taken for it.
        private void MarkDied(in CueEvent cue)
        {
            using var squads = _ends.ToEntityArray(Allocator.Temp);
            var nearest = Entity.Null;
            var best = CueReach;
            foreach (var squad in squads)
            {
                var end = EntityManager.GetComponentData<SquadEnd>(squad);
                var distance = math.distance(end.Position.xz, cue.Position.xz);
                if (end.TypeId == cue.TypeId && distance <= best)
                {
                    nearest = squad;
                    best = distance;
                }
            }

            if (nearest == Entity.Null)
            {
                return;
            }

            var died = EntityManager.GetComponentData<SquadEnd>(nearest);
            died.Died = true;
            EntityManager.SetComponentData(nearest, died);
        }

        // A ghost also goes when its squad walks into fog, so only one its death cue named leaves corpses.
        private void EndGoneSquads()
        {
            using var squads = _goneSquads.ToEntityArray(Allocator.Temp);
            foreach (var squad in squads)
            {
                var end = EntityManager.GetComponentData<SquadEnd>(squad);
                if (end.GoneAt < 0f && !end.Died)
                {
                    end.GoneAt = _now;
                    EntityManager.SetComponentData(squad, end);
                    continue;
                }

                if (end.Died)
                {
                    FallShownFigures(squad, end);
                }
                else if (_now < end.GoneAt + CueWait)
                {
                    continue;
                }

                EntityManager.RemoveComponent(squad, new ComponentTypeSet(typeof(SquadEnd), typeof(ShownFigure)));
            }
        }

        [BurstCompile]
        [WithAll(typeof(SquadFigures))]
        private partial struct ScanJob : IJobEntity
        {
            public float Now;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;
            public NativeList<Handover> Handovers;

            private void Execute(Entity entity, DynamicBuffer<SquadFigure> figures, in DynamicBuffer<SquadMember> members,
                DynamicBuffer<ShownFigure> shown, ref SquadEnd end, in LocalToWorld place, in EntityInfo info, in Owner owner)
            {
                end.TypeId = info.TypeId;
                end.Owner = owner.Value;
                end.Position = place.Position;
                end.GoneAt = -1f;
                var count = math.min(figures.Length, members.Length);
                shown.ResizeUninitialized(count);
                for (var i = 0; i < count; i++)
                {
                    ref var figure = ref figures.ElementAt(i);
                    if (figure.ReachesHandover(Now))
                    {
                        figure.HandedOver = true;
                        Handovers.Add(new Handover { Squad = entity, Figure = i });
                    }

                    shown[i] = ShownFigure.Create(figure, members[i], Places[figure.Figure].Value);
                }
            }
        }
    }
}
