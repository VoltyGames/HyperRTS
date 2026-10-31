using HyperRTS.Core;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
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
            state.RequireForUpdate<CueQueue>();
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
                Fades = SystemAPI.GetComponentLookup<VATFade>(),
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                CueEvents = SystemAPI.GetBufferLookup<CueEvent>(true),
                CueQueue = SystemAPI.GetSingletonEntity<CueQueue>(),
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
            public ComponentLookup<VATFade> Fades;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;
            [ReadOnly] public BufferLookup<CueEvent> CueEvents;
            public Entity CueQueue;
            public RenderSwitch Switch;

            private void Execute(Entity entity, ref SquadFigures squad, DynamicBuffer<SquadFigure> figures,
                in Health health, in LocalToWorld place)
            {
                // Unarmed squads never fire; the primary weapon is pose 0.
                var shots = Poses.TryGetBuffer(entity, out var poses) && poses.Length > 0 ? poses[0].Shots : (byte)0;
                var living = squad.Observe(place.Position, shots, Now, DeltaTime);
                squad.Hear(CueEvents[CueQueue], place.Position, Now);
                var visible = SquadFigures.VisibleCount(health, figures.Length);
                for (var i = 0; i < figures.Length; i++)
                {
                    var figure = figures[i];
                    var next = figure.Next(i < visible, living, Now);
                    if (next != figure.State)
                    {
                        Show(entity, ref figure, next, squad);
                        figures[i] = figure;
                    }
                    else if (next == FigureState.Die)
                    {
                        SwitchToBlast(figure, squad);
                    }
                }
            }

            private void Show(Entity entity, ref SquadFigure figure, FigureState next, in SquadFigures squad)
            {
                var hidden = next == FigureState.Hidden;
                Switch.SetBranch(entity, figure.Figure, HideReason.Figure, hidden);
                figure.State = next;
                if (hidden)
                {
                    return;
                }

                if (next == FigureState.Die)
                {
                    figure.HideAt = Now + squad.DieHold;
                }

                var clip = next == FigureState.Die ? DieClip(figure, squad) : figure.Clips.Clip(next);
                Play(figure, clip);
            }

            private VATClipRange DieClip(in SquadFigure figure, in SquadFigures squad)
            {
                if (!squad.Killed(Now))
                {
                    return figure.Clips.Die;
                }

                return BlastClip(figure, squad);
            }

            private VATClipRange BlastClip(in SquadFigure figure, in SquadFigures squad) =>
                figure.Clips.Blasted(squad.BlastPosition, Places[figure.Figure].Value);

            // A fall that began just before its blast's cue arrived changes to the blast fall.
            private void SwitchToBlast(in SquadFigure figure, in SquadFigures squad)
            {
                if (squad.BlastTime != Now || !squad.Killed(figure.HideAt - squad.DieHold))
                {
                    return;
                }

                var vat = Clips[figure.Figure];
                var clip = BlastClip(figure, squad);
                if (vat.Plays(figure.Clips.Die) && !vat.Plays(clip))
                {
                    Play(figure, clip);
                }
            }

            private void Play(in SquadFigure figure, in VATClipRange clip)
            {
                var vat = Clips[figure.Figure];
                var fade = Fades[figure.Figure];
                vat.Switch(clip, figure.StartTime(clip, Now), Now, ref fade);
                Clips[figure.Figure] = vat;
                Fades[figure.Figure] = fade;
            }
        }
    }
}
