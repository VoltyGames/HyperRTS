using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>One figure of a <see cref="SquadFigures"/> squad, showing the member at the same index.</summary>
    [InternalBufferCapacity(4)]
    public struct SquadFigure : IBufferElementData
    {
        /// <summary>Per second rate at which a figure closes on its member's offset.</summary>
        private const float FollowRate = 12f;

        /// <summary>Per second rate at which a figure turns to its cover heading, its aim or back.</summary>
        private const float TurnRate = 8f;

        /// <summary>Slowest and fastest a stride plays against its clip, so a stray speed neither freezes nor blurs the legs.</summary>
        private const float SlowestRate = 0.5f;

        private const float FastestRate = 1.6f;

        /// <summary>Share the stride rate must change by before it is rewritten, so a steady walk writes nothing.</summary>
        private const float RateStep = 0.03f;

        /// <summary>The model shown now: the one for the weapon its member fires.</summary>
        public Entity Figure;

        /// <summary>The clip ranges of the shown model, shared by every figure that plays the same set on the same table.</summary>
        public BlobAssetReference<FigureClips> Clips;

        /// <summary>The model shown for <see cref="FigureVariant.Reason"/> while it is not shown, and the base one while it is; null for none.</summary>
        public FigureVariant Other;

        /// <summary>True while <see cref="Figure"/> is the model <see cref="FigureVariant.Reason"/> asks for.</summary>
        public bool ShowsOther;

        /// <summary>0 to 1: how far into each looping clip this figure starts, so loops don't play in step.</summary>
        public float Phase;

        public FigureState State;

        public Posture Posture;

        /// <summary>True while the upper body plays its fire clip.</summary>
        public bool Firing;

        /// <summary>True while both halves of the body play the dig clip.</summary>
        public bool Digging;

        /// <summary>True on the frame its weapon fired, which restarts the fire clip.</summary>
        public bool Shot;

        /// <summary>The clip the legs play now, and when it started on the clock, which the upper body follows when it plays the same.</summary>
        public ClipRange Lower;

        public float LowerStart;

        /// <summary>Times the legs' clip speed it plays at, so strides match the ground the figure covers; the upper body follows it while it plays the same clip.</summary>
        public float LowerRate;

        /// <summary>Horizontal speed in m/s averaged over <see cref="SquadFigures.SpeedTime"/>.</summary>
        public float Speed;

        /// <summary>Clock time <see cref="State"/> last changed.</summary>
        public float StateSince;

        /// <summary>The clip the upper body plays now.</summary>
        public ClipRange Upper;

        /// <summary>Raised on each clip change, which its parts take on their own entities when it differs from the one they last took.</summary>
        public uint ClipEdits;

        /// <summary>What the last change did: switched the legs' or the upper body's clip, or only retimed them.</summary>
        public bool LowerPlayed;

        public bool UpperPlayed;
        public bool Retimed;

        /// <summary>Start and rate of the upper body's clip when it was switched.</summary>
        public float UpperStart;

        public float UpperRate;

        /// <summary>Clock time a dying figure hides at.</summary>
        public float HideAt;

        /// <summary>True once a dying figure's handover time has passed, whether it became a ragdoll or keeps its fall clip.</summary>
        public bool HandedOver;

        /// <summary>Where the figure stands in metres from the squad on world axes, eased toward its member's offset.</summary>
        public float2 Offset;

        /// <summary>Baked height of the shown model above its parent.</summary>
        public float Height;

        /// <summary>Baked rotation in its parent's frame, the squad's facing.</summary>
        public quaternion Baked;

        /// <summary>The way the figure faces in the world, eased, so a squad that turns at once doesn't turn its figures at once.</summary>
        public quaternion Facing;

        public float3 LastPosition;
        public ChangeTimer Shots;

        /// <summary>False until the first update records the position, so a spawn does not run.</summary>
        public bool Primed;

        public readonly bool HasOtherModel => Other.Model.Figure != Entity.Null;

        /// <summary>Whether the model <paramref name="member"/> calls for now, given whether the squad is <paramref name="emplaced"/>, is not the one shown.</summary>
        public readonly bool NeedsOtherModel(in SquadMember member, bool emplaced)
        {
            if (!HasOtherModel)
            {
                return false;
            }

            var wantsOther = Other.Reason == VariantReason.Emplaced ? member.Crews(emplaced) : member.Weapon != member.BaseWeapon;
            return ShowsOther != wantsOther;
        }

        /// <summary>Shows the other model; it is placed and posed afresh, as a figure not yet shown.</summary>
        public void SwapModel()
        {
            var shown = new FigureModel { Figure = Figure, Clips = Clips, Height = Height };
            Figure = Other.Model.Figure;
            Clips = Other.Model.Clips;
            Height = Other.Model.Height;
            Other.Model = shown;
            ShowsOther = !ShowsOther;
            State = FigureState.Unset;
            Lower = default;
            Upper = default;
            LowerRate = 1f;
        }

        /// <summary>Forgets the last frame's clip change before this frame's.</summary>
        public void ClearEdit()
        {
            LowerPlayed = false;
            UpperPlayed = false;
            Retimed = false;
        }

        /// <summary>Records that the parts switch to <see cref="Lower"/> and <see cref="Upper"/>, as far as asked.</summary>
        public void MarkPlayed(bool lower, bool upper, float upperStart, float upperRate)
        {
            LowerPlayed = lower;
            UpperPlayed = upper;
            UpperStart = upperStart;
            UpperRate = upperRate;
            ClipEdits++;
        }

        /// <summary>Records that the parts take <see cref="LowerStart"/> and <see cref="LowerRate"/> without switching.</summary>
        public void MarkRetimed()
        {
            Retimed = true;
            ClipEdits++;
        }

        /// <summary>Whether the clips played were chosen for another posture, firing or digging than these.</summary>
        public readonly bool PoseDiffers(Posture posture, bool firing, bool digging) =>
            Firing != firing || Posture != posture || Digging != digging;

        /// <summary>Times its own speed that <paramref name="clip"/> plays so its feet keep to the ground at the figure's speed.</summary>
        public readonly float RateFor(in ClipRange clip)
        {
            if (clip.GroundSpeed <= 0f)
            {
                return 1f;
            }

            return math.clamp(Speed / clip.GroundSpeed, SlowestRate, FastestRate);
        }

        /// <summary>
        /// Moves the legs' clip to the rate for the speed now, keeping its phase, and returns true when it changed
        /// by more than <see cref="RateStep"/>.
        /// </summary>
        public bool Retune(float now)
        {
            var wanted = RateFor(Lower);
            if (Lower.FrameCount == 0 || math.abs(wanted / LowerRate - 1f) <= RateStep)
            {
                return false;
            }

            LowerStart = now - (now - LowerStart) * LowerRate / wanted;
            LowerRate = wanted;
            return true;
        }

        /// <summary>Eases <see cref="Offset"/> toward <paramref name="target"/>, or jumps there on the first update.</summary>
        public void Follow(float2 target, float deltaTime)
        {
            Offset = Primed ? math.lerp(Offset, target, 1f - math.exp(-FollowRate * deltaTime)) : target;
        }

        /// <summary>
        /// Eases <see cref="Facing"/> toward its weapon's <paramref name="aim"/> (radians off the squad's heading) while it
        /// fires, else the way <paramref name="member"/> faces in cover, else the baked turn from its parent's world
        /// rotation <paramref name="parentRotation"/>; jumps there on the first update.
        /// </summary>
        public void Turn(quaternion parentRotation, in SquadMember member, float aim, float deltaTime)
        {
            var target = math.mul(parentRotation, Baked);
            if (Firing)
            {
                target = math.mul(math.mul(parentRotation, quaternion.RotateY(aim)), Baked);
            }
            else if (member.Cover != CoverKind.None)
            {
                var facing = member.CoverFacing;
                target = math.mul(quaternion.LookRotationSafe(new float3(facing.x, 0f, facing.y), math.up()), Baked);
            }

            Facing = Primed ? math.slerp(Facing, target, 1f - math.exp(-TurnRate * deltaTime)) : target;
        }

        /// <summary>Records this frame's position and shot count; returns how a living figure moves and whether it fires.</summary>
        public FigureState Observe(in SquadFigures squad, float3 position, byte shots, float now, float deltaTime,
            float fireHold, out bool firing)
        {
            if (!Primed)
            {
                Primed = true;
                LastPosition = position;
            }

            var measured = deltaTime > 0f ? math.distance(position.xz, LastPosition.xz) / deltaTime : 0f;
            Speed = math.lerp(Speed, measured, 1f - math.exp(-deltaTime / SquadFigures.SpeedTime));
            Shot = Shots.Update(shots, deltaTime);
            LastPosition = position;
            firing = Shots.Within(fireHold);
            return squad.Moving(Speed, State, now - StateSince);
        }

        /// <summary>The state this figure moves to: a dead member's figure dies then hides, a living one's shows <paramref name="living"/>.</summary>
        public readonly FigureState Next(bool alive, FigureState living, float now)
        {
            if (alive)
            {
                return living;
            }

            // A figure already gone when first placed, as on a damaged spawn, skips its fall.
            if (State is FigureState.Unset or FigureState.Hidden)
            {
                return FigureState.Hidden;
            }

            if (State != FigureState.Die)
            {
                return FigureState.Die;
            }

            return now >= HideAt ? FigureState.Hidden : FigureState.Die;
        }

        /// <summary>Whether this dying figure reaches its handover from the fall clip at <paramref name="now"/>.</summary>
        public readonly bool ReachesHandover(float now)
        {
            if (State != FigureState.Die || HandedOver)
            {
                return false;
            }

            return now >= StateSince + Clips.Value.Ragdoll.Tuning.handoverTime;
        }

        /// <summary>When <paramref name="clip"/> started for this figure if it began at <paramref name="now"/>, played at <paramref name="rate"/>.</summary>
        public readonly float StartTime(in ClipRange clip, float now, float rate)
        {
            var length = clip.FrameCount / (clip.Fps * rate);
            return clip.Loop ? now - Phase * length : now;
        }
    }
}
