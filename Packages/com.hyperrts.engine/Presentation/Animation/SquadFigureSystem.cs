using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Shows one <see cref="SquadFigure"/> per share of health and plays each the clip for the squad's motion and
    /// firing.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SquadFigureSystem : ISystem
    {
        private RenderSwitch _switch;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            state.RequireForUpdate<SquadFigures>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            state.Dependency = new SquadJob
            {
                Now = (float)SystemAPI.Time.ElapsedTime,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Clips = SystemAPI.GetComponentLookup<VATClip>(),
                Switch = _switch,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct SquadJob : IJobEntity
        {
            public float Now;
            public float DeltaTime;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            public ComponentLookup<VATClip> Clips;
            public RenderSwitch Switch;

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
                        Show(entity, ref figure, next, squad.DieHold);
                        figures[i] = figure;
                    }
                }
            }

            private void Show(Entity squad, ref SquadFigure figure, FigureState next, float dieHold)
            {
                var hidden = next == FigureState.Hidden;
                Switch.SetBranch(squad, figure.Figure, HideReason.Figure, hidden);
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
