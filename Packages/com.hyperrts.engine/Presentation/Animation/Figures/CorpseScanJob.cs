using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Records where each squad is and lists the figures that reached their handover.</summary>
    [BurstCompile]
    [WithAll(typeof(SquadFigures))]
    internal partial struct CorpseScanJob : IJobEntity
    {
        public float Now;
        [ReadOnly] public ComponentLookup<LocalToWorld> Places;
        public NativeList<CorpseHandover> Handovers;

        private void Execute(Entity entity, DynamicBuffer<SquadFigure> figures, in DynamicBuffer<SquadMember> members,
            DynamicBuffer<ShownFigure> shown, ref SquadEnd end, in EntityInfo info, in SpawnNumber number,
            in Owner owner)
        {
            end.TypeId = info.TypeId;
            end.SpawnNumber = number.Value;
            end.Owner = owner.Value;
            end.GoneAt = -1f;
            // A ghost that outlasts the cue naming it wasn't the squad that died.
            end.Died &= Now < end.DiedAt + SquadEnd.CueWait;
            var count = math.min(figures.Length, members.Length);
            shown.ResizeUninitialized(count);
            for (var i = 0; i < count; i++)
            {
                ref var figure = ref figures.ElementAt(i);
                var world = Places[figure.Figure].Value;
                if (figure.ReachesHandover(Now))
                {
                    figure.HandedOver = true;
                    Handovers.Add(new CorpseHandover { Squad = entity, Figure = i, Member = members[i], World = world });
                }

                shown[i] = ShownFigure.Create(figure, members[i], world);
            }
        }
    }
}
