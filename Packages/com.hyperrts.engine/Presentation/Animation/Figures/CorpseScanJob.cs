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
                    Handovers.Add(new CorpseHandover { Squad = entity, Figure = i });
                }

                shown[i] = ShownFigure.Create(figure, members[i], Places[figure.Figure].Value);
            }
        }
    }
}
