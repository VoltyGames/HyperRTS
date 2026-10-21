using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Shows one <see cref="SquadFigure"/> per share of health and plays each the clip for the squad's motion and
    /// firing. Hidden figures scale to zero, leaving fog's rendering toggles alone.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SquadFigureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SquadFigures>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new SquadJob
            {
                Now = (float)SystemAPI.Time.ElapsedTime,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Clips = SystemAPI.GetComponentLookup<VATClip>(),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct SquadJob : IJobEntity
        {
            public float Now;
            public float DeltaTime;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            public ComponentLookup<VATClip> Clips;
            public ComponentLookup<LocalTransform> Transforms;

            private void Execute(Entity entity, ref SquadFigures squad, DynamicBuffer<SquadFigure> figures,
                in Health health, in LocalToWorld place)
            {
                // Unarmed squads never fire; the primary weapon is pose 0.
                var shots = Poses.TryGetBuffer(entity, out var poses) && poses.Length > 0 ? poses[0].Shots : (byte)0;
                var living = squad.Observe(place.Position, shots, Now, DeltaTime);
                var visible = SquadFigures.VisibleCount(health, figures.Length);
                for (var i = 0; i < figures.Length; i++)
                {
                    var figure = figures[i];
                    var next = figure.Next(i < visible, living, Now);
                    if (next != figure.State)
                    {
                        Show(ref figure, next, squad.DieHold);
                        figures[i] = figure;
                    }
                }
            }

            private void Show(ref SquadFigure figure, FigureState next, float dieHold)
            {
                var hidden = next == FigureState.Hidden;
                Transforms.GetRefRW(figure.Figure).ValueRW.Scale = hidden ? 0f : figure.Scale;
                figure.State = next;
                if (hidden)
                {
                    return;
                }

                if (next == FigureState.Die)
                {
                    figure.HideAt = Now + dieHold;
                }

                var clip = figure.Clips.Clip(next);
                Clips[figure.Figure] = VATClip.Started(clip, figure.StartTime(clip, Now));
            }
        }
    }
}
