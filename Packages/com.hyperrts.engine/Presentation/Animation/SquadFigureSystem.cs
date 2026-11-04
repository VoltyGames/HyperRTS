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
    /// Puts each <see cref="SquadFigure"/> where its <see cref="SquadMember"/> stands, eased, and plays every renderer of
    /// it the clips for its own motion, posture and weapon: the legs follow motion and posture, the upper body fires over
    /// them. A dead member's figure dies, then hides.
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
                Lowers = SystemAPI.GetComponentLookup<LowerClip>(),
                Uppers = SystemAPI.GetComponentLookup<UpperClip>(),
                LowerFades = SystemAPI.GetComponentLookup<LowerFade>(),
                UpperFades = SystemAPI.GetComponentLookup<UpperFade>(),
                Linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
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
            public ComponentLookup<LowerClip> Lowers;
            public ComponentLookup<UpperClip> Uppers;
            public ComponentLookup<LowerFade> LowerFades;
            public ComponentLookup<UpperFade> UpperFades;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Linked;
            [ReadOnly] public BufferLookup<Child> Children;
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
                var parts = new NativeList<Entity>(Allocator.Temp);
                RenderHierarchy.Collect(entity, Linked, Children, parts);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    var figure = figures[i];
                    var moving = FigureState.Idle;
                    var firing = false;
                    if (member.Alive)
                    {
                        figure.Follow(member.Offset, DeltaTime);
                        Stand(figure);
                        var shots = hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].Shots : (byte)0;
                        var feet = place.Position + new float3(figure.Offset.x, 0f, figure.Offset.y);
                        moving = figure.Observe(squad, feet, shots, Now, DeltaTime, out firing);
                    }

                    var next = figure.Next(member.Alive, moving, Now);
                    if (next != figure.State)
                    {
                        Show(entity, parts, ref figure, next, member.Posture, firing, squad);
                    }
                    else if (next == FigureState.Die)
                    {
                        SwitchToBlast(parts, ref figure, squad);
                    }
                    else if (next != FigureState.Hidden && (figure.Firing != firing || figure.Posture != member.Posture))
                    {
                        Pose(parts, ref figure, next, member.Posture, firing);
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

            private void Show(Entity entity, NativeList<Entity> parts, ref SquadFigure figure, FigureState next, Posture posture,
                bool firing, in SquadFigures squad)
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
                    Fall(parts, ref figure, DieClip(figure, squad));
                    return;
                }

                Pose(parts, ref figure, next, posture, firing);
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
            private void SwitchToBlast(NativeList<Entity> parts, ref SquadFigure figure, in SquadFigures squad)
            {
                if (squad.BlastTime != Now || !squad.Killed(figure.HideAt - squad.DieHold))
                {
                    return;
                }

                var clip = BlastClip(figure, squad);
                if (figure.Lower.SameAs(figure.Clips.Die) && !figure.Lower.SameAs(clip))
                {
                    Fall(parts, ref figure, clip);
                }
            }

            // The legs follow motion and posture; the upper body fires over them or keeps their clip, in step with them.
            private void Pose(NativeList<Entity> parts, ref SquadFigure figure, FigureState moving, Posture posture, bool firing)
            {
                figure.Posture = posture;
                figure.Firing = firing;
                var lower = figure.Clips.Lower(posture, moving);
                var upper = figure.Clips.Upper(posture, moving, firing);
                var lowerChanged = !figure.Lower.SameAs(lower);
                if (lowerChanged)
                {
                    figure.Lower = lower;
                    figure.LowerStart = figure.StartTime(lower, Now);
                }

                var upperChanged = !figure.Upper.SameAs(upper);
                figure.Upper = upper;
                var upperStart = upper.SameAs(lower) ? figure.LowerStart : figure.StartTime(upper, Now);
                Play(parts, figure, lowerChanged ? lower : default, upperChanged ? upper : default, upperStart);
            }

            private void Fall(NativeList<Entity> parts, ref SquadFigure figure, in VATClipRange clip)
            {
                figure.Lower = clip;
                figure.Upper = clip;
                figure.LowerStart = Now;
                Play(parts, figure, clip, clip, Now);
            }

            // A range with no frames leaves that half as it is.
            private void Play(NativeList<Entity> parts, in SquadFigure figure, in VATClipRange lower, in VATClipRange upper,
                float upperStart)
            {
                foreach (var part in parts)
                {
                    if (!Lowers.HasComponent(part) || !Under(part, figure.Figure))
                    {
                        continue;
                    }

                    if (lower.FrameCount > 0)
                    {
                        var low = Lowers[part];
                        var fade = LowerFades[part];
                        low.Clip.Switch(lower, figure.LowerStart, Now, ref fade.Fade);
                        Lowers[part] = low;
                        LowerFades[part] = fade;
                    }

                    if (upper.FrameCount > 0)
                    {
                        var high = Uppers[part];
                        var fade = UpperFades[part];
                        high.Clip.Switch(upper, upperStart, Now, ref fade.Fade);
                        Uppers[part] = high;
                        UpperFades[part] = fade;
                    }
                }
            }

            private bool Under(Entity part, Entity figure)
            {
                for (var step = 0; step < 8 && part != Entity.Null; step++)
                {
                    if (part == figure)
                    {
                        return true;
                    }

                    part = Parents.TryGetComponent(part, out var parent) ? parent.Value : Entity.Null;
                }

                return false;
            }
        }
    }
}
