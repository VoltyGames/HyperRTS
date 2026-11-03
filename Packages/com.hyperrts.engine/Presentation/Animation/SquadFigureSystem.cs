using HyperRTS.Core;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Puts each <see cref="SquadFigure"/> where its <see cref="SquadMember"/> stands, eased, and plays it the clip for
    /// its own motion and weapon. A dead member's figure dies, then hides.
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
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Spots = SystemAPI.GetComponentLookup<LocalTransform>(),
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
            [ReadOnly] public ComponentLookup<Parent> Parents;
            public ComponentLookup<LocalTransform> Spots;
            public ComponentLookup<VATClip> Clips;
            public ComponentLookup<VATFade> Fades;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;
            [ReadOnly] public BufferLookup<CueEvent> CueEvents;
            public Entity CueQueue;
            public RenderSwitch Switch;

            private void Execute(Entity entity, ref SquadFigures squad, DynamicBuffer<SquadFigure> figures,
                in DynamicBuffer<SquadMember> members, in LocalToWorld place)
            {
                squad.Hear(CueEvents[CueQueue], place.Position, Now);
                var hasPoses = Poses.TryGetBuffer(entity, out var poses);
                var count = math.min(figures.Length, members.Length);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    var figure = figures[i];
                    var living = FigureState.Idle;
                    if (member.Alive)
                    {
                        figure.Follow(member.Offset, DeltaTime);
                        Stand(figure);
                        var shots = hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].Shots : (byte)0;
                        var feet = place.Position + new float3(figure.Offset.x, 0f, figure.Offset.y);
                        living = figure.Observe(squad, feet, shots, Now, DeltaTime);
                    }

                    var next = figure.Next(member.Alive, living, Now);
                    if (next != figure.State)
                    {
                        Show(entity, ref figure, next, squad);
                    }
                    else if (next == FigureState.Die)
                    {
                        SwitchToBlast(figure, squad);
                    }

                    figures[i] = figure;
                }
            }

            /// <summary>Moves the figure to its offset: world axes turned into its parent's frame, at its baked height.</summary>
            private void Stand(in SquadFigure figure)
            {
                var parent = Places[Parents[figure.Figure].Value].Value;
                var local = math.mul(math.inverse(new float3x3(parent)), new float3(figure.Offset.x, 0f, figure.Offset.y));
                var spot = Spots[figure.Figure];
                spot.Position = new float3(local.x, figure.Height, local.z);
                Spots[figure.Figure] = spot;
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
