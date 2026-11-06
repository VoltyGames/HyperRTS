using HyperRTS.Core;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Plays every renderer of each <see cref="SquadFigure"/> the clips for its own motion, posture and weapon: the legs
    /// follow motion and posture, the upper body fires over them. A dead member's figure dies, then hides;
    /// <see cref="SquadFigurePlacementSystem"/> places them.
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
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                Lowers = SystemAPI.GetComponentLookup<LowerClip>(),
                Uppers = SystemAPI.GetComponentLookup<UpperClip>(),
                LowerFades = SystemAPI.GetComponentLookup<LowerFade>(),
                UpperFades = SystemAPI.GetComponentLookup<UpperFade>(),
                Renderers = SystemAPI.GetBufferLookup<FigureRenderer>(true),
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
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            public ComponentLookup<LowerClip> Lowers;
            public ComponentLookup<UpperClip> Uppers;
            public ComponentLookup<LowerFade> LowerFades;
            public ComponentLookup<UpperFade> UpperFades;
            [ReadOnly] public BufferLookup<FigureRenderer> Renderers;
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
                var crewing = Deployables.TryGetComponent(entity, out var deployable) && deployable.Emplaced;
                ShowDeployedModel(entity, ref squad, crewing);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    var figure = figures[i];
                    if (member.Alive && figure.NeedsOtherModel(member, crewing))
                    {
                        figure.SwapModel();
                    }

                    if (figure.State == FigureState.Unset && figure.HasOtherModel)
                    {
                        Switch.SetBranch(entity, figure.Other.Model.Figure, HideReason.Figure, true);
                    }

                    var crewed = crewing && figure.Crew.Takes;
                    var posture = crewed ? figure.Crew.Posture : member.Posture;
                    var moving = FigureState.Idle;
                    var firing = false;
                    var digging = false;
                    if (member.Alive)
                    {
                        var shots = hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].Shots : (byte)0;
                        var feet = place.Position + new float3(figure.Offset.x, 0f, figure.Offset.y);
                        moving = figure.Observe(squad, feet, shots, Now, DeltaTime, out firing);
                        digging = !crewed && IsDigging(member, moving, firing);
                    }

                    var next = figure.Next(member.Alive, moving, Now);
                    Animate(entity, ref figure, next, posture, firing, digging, squad);

                    figures[i] = figure;
                }
            }

            private void Animate(Entity root, ref SquadFigure figure, FigureState next, Posture posture,
                bool firing, bool digging, in SquadFigures squad)
            {
                if (next != figure.State)
                {
                    Show(root, ref figure, next, posture, firing, digging, squad);
                }
                else if (next == FigureState.Die)
                {
                    SwitchToBlast(root, ref figure, squad);
                }
                else if (next != FigureState.Hidden && figure.PoseDiffers(posture, firing, digging))
                {
                    Pose(root, ref figure, next, posture, firing, digging);
                }

                if (next is FigureState.Walk or FigureState.Run && figure.Retune(Now))
                {
                    Retime(root, figure);
                }
            }

            // The model shows from the start of deploying until packing up; hiding it is a no-op on a squad that has none.
            private void ShowDeployedModel(Entity root, ref SquadFigures squad, bool deployed)
            {
                if (squad.DeployedModel == Entity.Null || squad.ModelHidden == !deployed)
                {
                    return;
                }

                squad.ModelHidden = !deployed;
                Switch.SetBranch(root, squad.DeployedModel, HideReason.Figure, squad.ModelHidden);
            }

            private void Show(Entity root, ref SquadFigure figure, FigureState next, Posture posture,
                bool firing, bool digging, in SquadFigures squad)
            {
                var hidden = next == FigureState.Hidden;
                Switch.SetBranch(root, figure.Figure, HideReason.Figure, hidden);
                figure.State = next;
                figure.StateSince = Now;
                if (hidden)
                {
                    return;
                }

                if (next == FigureState.Die)
                {
                    figure.HideAt = Now + squad.DieHold;
                    Fall(root, ref figure, DieClip(figure, squad));
                    return;
                }

                Pose(root, ref figure, next, posture, firing, digging);
            }

            // A standing member at its place on a site digs unless it is firing.
            private static bool IsDigging(in SquadMember member, FigureState moving, bool firing)
            {
                var standing = member.Posture == Posture.Stand && moving == FigureState.Idle;
                return standing && !firing && member.Action == MemberAction.Dig;
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

            // A fall that began just before its blast cue arrived changes to the blast fall.
            private void SwitchToBlast(Entity root, ref SquadFigure figure, in SquadFigures squad)
            {
                if (squad.BlastTime != Now || !squad.Killed(figure.HideAt - squad.DieHold))
                {
                    return;
                }

                var clip = BlastClip(figure, squad);
                if (figure.Lower.SameAs(figure.Clips.Die) && !figure.Lower.SameAs(clip))
                {
                    Fall(root, ref figure, clip);
                }
            }
        }
    }
}
