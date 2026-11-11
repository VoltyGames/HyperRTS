using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Plays each <see cref="SquadFigure"/>'s renderers the clips for its motion, posture and weapon; a dead member's
    /// figure dies, then hides. <see cref="SquadFigurePlacementSystem"/> places them.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SquadFigureSystem : ISystem
    {
        private RenderSwitch _switch;
        private EntityQuery _parts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            _parts = SystemAPI.QueryBuilder().WithAll<FigurePart, LowerClip, UpperClip, LowerFade, UpperFade>().Build();
            state.RequireForUpdate<SquadFigures>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            var now = (float)SystemAPI.Time.ElapsedTime;
            var hides = new NativeQueue<FigureHide>(state.WorldUpdateAllocator);
            var squads = new SquadJob
            {
                Now = now,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Hides = hides.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
            var hide = new HideJob { Hides = hides, Switch = _switch }.Schedule(squads);
            var parts = new PartJob
            {
                Now = now,
                Figures = SystemAPI.GetBufferLookup<SquadFigure>(true),
                Parts = SystemAPI.GetComponentTypeHandle<FigurePart>(),
                Lowers = SystemAPI.GetComponentTypeHandle<LowerClip>(),
                Uppers = SystemAPI.GetComponentTypeHandle<UpperClip>(),
                LowerFades = SystemAPI.GetComponentTypeHandle<LowerFade>(),
                UpperFades = SystemAPI.GetComponentTypeHandle<UpperFade>(),
            }.ScheduleParallel(_parts, squads);
            state.Dependency = JobHandle.CombineDependencies(hide, parts);
        }

        [BurstCompile]
        private partial struct SquadJob : IJobEntity
        {
            public float Now;
            public float DeltaTime;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;

            // Each squad writes only itself: its figures' clip changes, which their parts pull, and the hides it queues.
            public NativeQueue<FigureHide>.ParallelWriter Hides;

            private void Execute(Entity entity, ref SquadFigures squad, DynamicBuffer<SquadFigure> figures,
                in DynamicBuffer<SquadMember> members, in LocalToWorld place)
            {
                var hasPoses = Poses.TryGetBuffer(entity, out var poses);
                var count = math.min(figures.Length, members.Length);
                var crewing = Deployable.IsEmplaced(Deployables, entity);
                ShowDeployedModel(entity, ref squad, crewing);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    var figure = figures[i];
                    figure.ClearEdit();
                    if (member.Alive && figure.NeedsOtherModel(member, crewing))
                    {
                        figure.SwapModel();
                    }

                    if (figure.State == FigureState.Unset && figure.HasOtherModel)
                    {
                        Hides.Enqueue(new FigureHide { Root = entity, Branch = figure.Other.Model.Figure, Hide = true });
                    }

                    var moving = FigureState.Idle;
                    var firing = false;
                    var digging = false;
                    if (member.Alive)
                    {
                        var shots = hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].Shots : (byte)0;
                        var feet = place.Position + new float3(figure.Offset.x, 0f, figure.Offset.y);
                        var hold = figure.Clips.Value.FireHold(member.Posture);
                        moving = figure.Observe(squad, feet, shots, Now, DeltaTime, hold, out firing);
                        digging = IsDigging(member, moving, firing);
                    }

                    var next = figure.Next(member.Alive, moving, Now);
                    Animate(entity, ref figure, next, member, firing, digging, squad);

                    figures[i] = figure;
                }
            }

            private void Animate(Entity root, ref SquadFigure figure, FigureState next, in SquadMember member,
                bool firing, bool digging, in SquadFigures squad)
            {
                if (next != figure.State)
                {
                    Show(root, ref figure, next, member, firing, digging, squad);
                }
                else if (next != FigureState.Die && next != FigureState.Hidden
                    && (figure.PoseDiffers(member.Posture, firing, digging) || firing && figure.Shot))
                {
                    Pose(ref figure, next, member.Posture, firing, digging);
                }

                if (next is FigureState.Walk or FigureState.Run && figure.Retune(Now))
                {
                    figure.MarkRetimed();
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
                Hides.Enqueue(new FigureHide { Root = root, Branch = squad.DeployedModel, Hide = squad.ModelHidden });
            }

            private void Show(Entity root, ref SquadFigure figure, FigureState next, in SquadMember member,
                bool firing, bool digging, in SquadFigures squad)
            {
                var hidden = next == FigureState.Hidden;
                Hides.Enqueue(new FigureHide { Root = root, Branch = figure.Figure, Hide = hidden });
                figure.State = next;
                figure.StateSince = Now;
                if (hidden)
                {
                    return;
                }

                if (next == FigureState.Die)
                {
                    figure.HideAt = Now + squad.DieHold;
                    Fall(ref figure, DieClip(figure, member));
                    return;
                }

                Pose(ref figure, next, member.Posture, firing, digging);
            }

            // A standing member at its place on a site digs unless it is firing.
            private static bool IsDigging(in SquadMember member, FigureState moving, bool firing)
            {
                var standing = member.Posture == Posture.Stand && moving == FigureState.Idle;
                return standing && !firing && member.Action == MemberAction.Dig;
            }

            private ClipRange DieClip(in SquadFigure figure, in SquadMember member)
            {
                var direction = member.BlastDirection;
                return figure.Clips.Value.Fall(member.Posture, member.Death, new float3(direction.x, 0f, direction.y),
                    Places[figure.Figure].Value);
            }
        }
    }
}
