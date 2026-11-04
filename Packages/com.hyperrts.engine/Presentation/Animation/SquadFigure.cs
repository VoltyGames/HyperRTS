using Unity.Collections;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>One figure of a <see cref="SquadFigures"/> squad, showing the member at the same index.</summary>
    [InternalBufferCapacity(4)]
    public struct SquadFigure : IBufferElementData
    {
        /// <summary>Per second rate at which a figure closes on its member's offset.</summary>
        private const float FollowRate = 12f;

        /// <summary>Per second rate at which a figure turns to its cover heading or back.</summary>
        private const float TurnRate = 8f;

        /// <summary>The model shown now: the one for the weapon its member fires.</summary>
        public Entity Figure;

        /// <summary>The weapon's designation for the HUD's member card; baked, never sent.</summary>
        public FixedString32Bytes Weapon;

        /// <summary>Figures baked separately keep their own tables, so each carries its own clip ranges.</summary>
        public FigureClips Clips;

        /// <summary>The model for the member's upgraded weapon while it is not shown, and the base one while it is; null for none.</summary>
        public FigureVariant Other;

        /// <summary>True while <see cref="Figure"/> is the upgraded model.</summary>
        public bool ShowsUpgraded;

        /// <summary>Where the figure stands while its squad's weapon is deployed.</summary>
        public CrewPlace Crew;

        /// <summary>0 to 1: how far into each looping clip this figure starts, so loops don't play in step.</summary>
        public float Phase;

        public FigureState State;

        public Posture Posture;

        /// <summary>True while the upper body plays its fire clip.</summary>
        public bool Firing;

        /// <summary>True while both halves of the body play the dig clip.</summary>
        public bool Digging;

        /// <summary>The clip the legs play now, and when it started on the clock, which the upper body follows when it plays the same.</summary>
        public VATClipRange Lower;

        public float LowerStart;

        /// <summary>Horizontal speed in m/s averaged over <see cref="SquadFigures.SpeedTime"/>.</summary>
        public float Speed;

        /// <summary>Clock time <see cref="State"/> last changed.</summary>
        public float StateSince;

        /// <summary>The clip the upper body plays now.</summary>
        public VATClipRange Upper;

        /// <summary>Clock time a dying figure hides at.</summary>
        public float HideAt;

        /// <summary>Where the figure stands in metres from the squad on world axes, eased toward its member's offset.</summary>
        public float2 Offset;

        /// <summary>Baked height of the shown model above its parent.</summary>
        public float Height;

        /// <summary>Baked rotation in its parent's frame, the squad's facing; set on the first update.</summary>
        public quaternion Baked;

        public float3 LastPosition;
        public byte LastShots;
        public float LastShotTime;

        /// <summary>False until the first update records position and shots, so a spawn neither runs nor fires.</summary>
        public bool Primed;

        public readonly bool HasUpgradedModel => Other.Figure != Entity.Null;

        /// <summary>Whether the model for the weapon <paramref name="member"/> fires now is not the one shown.</summary>
        public readonly bool NeedsOtherModel(in SquadMember member) =>
            HasUpgradedModel && ShowsUpgraded != (member.Weapon != member.BaseWeapon);

        /// <summary>Shows the other model; it is placed and posed afresh, as a figure not yet shown.</summary>
        public void SwapModel()
        {
            var shown = new FigureVariant { Figure = Figure, Clips = Clips, Weapon = Weapon, Height = Height };
            Figure = Other.Figure;
            Clips = Other.Clips;
            Weapon = Other.Weapon;
            Height = Other.Height;
            Other = shown;
            ShowsUpgraded = !ShowsUpgraded;
            State = FigureState.Unset;
            Lower = default;
            Upper = default;
        }

        /// <summary>Eases <see cref="Offset"/> toward <paramref name="target"/>, or jumps there on the first update.</summary>
        public void Follow(float2 target, float deltaTime)
        {
            Offset = Primed ? math.lerp(Offset, target, 1f - math.exp(-FollowRate * deltaTime)) : target;
        }

        /// <summary>
        /// The rotation to ease to: toward the way <paramref name="member"/> faces in cover, else back to the baked one.
        /// <paramref name="worldToParent"/> turns a world direction into the figure's parent frame.
        /// </summary>
        public readonly quaternion Turned(quaternion current, in SquadMember member, float3x3 worldToParent, float deltaTime)
        {
            var target = Baked;
            if (member.Cover != CoverKind.None)
            {
                var facing = member.CoverFacing;
                var local = math.mul(worldToParent, new float3(facing.x, 0f, facing.y));
                target = math.mul(quaternion.LookRotationSafe(local, math.up()), Baked);
            }

            return Primed ? math.slerp(current, target, 1f - math.exp(-TurnRate * deltaTime)) : target;
        }

        /// <summary>Records this frame's world position and weapon shot count; returns how a living figure moves and whether it fires.</summary>
        public FigureState Observe(in SquadFigures squad, float3 position, byte shots, float now, float deltaTime, out bool firing)
        {
            if (!Primed)
            {
                Primed = true;
                LastPosition = position;
                LastShots = shots;
                LastShotTime = now - SquadFigures.FireHold;
            }

            var measured = deltaTime > 0f ? math.distance(position.xz, LastPosition.xz) / deltaTime : 0f;
            Speed = math.lerp(Speed, measured, 1f - math.exp(-deltaTime / SquadFigures.SpeedTime));
            if (shots != LastShots)
            {
                LastShotTime = now;
            }

            LastPosition = position;
            LastShots = shots;
            firing = now - LastShotTime < SquadFigures.FireHold;
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

        /// <summary>When <paramref name="clip"/> started for this figure if it began at <paramref name="now"/>.</summary>
        public readonly float StartTime(in VATClipRange clip, float now)
        {
            var length = clip.FrameCount / clip.Fps;
            return clip.Loop ? now - Phase * length : now;
        }
    }
}
