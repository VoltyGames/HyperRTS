using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Plays each squad's figures: swaps models, picks clips for motion, posture and fire, and queues hides.</summary>
    [BurstCompile]
    internal partial struct SquadFigureJob : IJobEntity
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

        // The legs follow motion and posture; the upper body fires over them or keeps their clip, in step with them.
        private void Pose(ref SquadFigure figure, FigureState moving, Posture posture, bool firing,
            bool digging)
        {
            figure.Posture = posture;
            figure.Firing = firing;
            figure.Digging = digging;
            ref var clips = ref figure.Clips.Value;
            var lower = digging ? clips.Action(MemberAction.Dig) : clips.Lower(posture, moving);
            var upper = digging ? clips.Action(MemberAction.Dig) : clips.Upper(posture, moving, firing);
            var lowerChanged = !figure.Lower.SameAs(lower);
            if (lowerChanged)
            {
                figure.Lower = lower;
                figure.LowerRate = figure.RateFor(lower);
                figure.LowerStart = figure.StartTime(lower, Now, figure.LowerRate);
            }

            var upperChanged = figure.Shot && firing || !figure.Upper.SameAs(upper);
            figure.Upper = upper;
            var upperStart = figure.LowerStart;
            var upperRate = figure.LowerRate;
            if (!upper.SameAs(lower))
            {
                upperRate = 1f;
                upperStart = figure.StartTime(upper, Now, upperRate);
            }

            figure.MarkPlayed(lowerChanged, upperChanged, upperStart, upperRate);
        }

        private void Fall(ref SquadFigure figure, in ClipRange clip)
        {
            figure.Lower = clip;
            figure.Upper = clip;
            figure.LowerStart = Now;
            figure.LowerRate = 1f;
            figure.MarkPlayed(true, true, Now, 1f);
        }
    }
}
