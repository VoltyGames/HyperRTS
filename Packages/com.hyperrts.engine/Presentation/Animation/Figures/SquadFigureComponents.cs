using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// A squad drawn as several bone-animated figures under one unit: figure i shows squad member i, standing at its
    /// offset. Its legs play idle, walk or run for its own motion and posture, its upper body fires over them, and it
    /// dies when the member does. Client only.
    /// </summary>
    public struct SquadFigures : IComponentData
    {
        /// <summary>Below this horizontal speed in m/s figures stand idle, so ghost jitter doesn't walk them.</summary>
        public const float WalkSpeed = 0.2f;

        /// <summary>Horizontal speed in m/s from which figures run instead of walk.</summary>
        public float RunSpeed;

        /// <summary>Seconds a dropped figure shows its die clip before it hides.</summary>
        public float DieHold;

        /// <summary>The deployed weapon's model, a child of the unit shown while it is set up; null for none.</summary>
        public Entity DeployedModel;

        /// <summary>True while the deployed model is hidden; it starts shown, so the first update hides it.</summary>
        public bool ModelHidden;

        /// <summary>Seconds over which a figure's measured speed averages, since a squad moves in simulation steps.</summary>
        public const float SpeedTime = 0.25f;

        /// <summary>Seconds a figure keeps a motion before it may change to another, so a borderline speed doesn't flicker.</summary>
        public const float MoveHold = 0.3f;

        /// <summary>Share of <see cref="WalkSpeed"/> below which a walking figure stops.</summary>
        public const float StopShare = 0.5f;

        /// <summary>Share of <see cref="RunSpeed"/> below which a running figure slows to a walk.</summary>
        public const float SlowShare = 0.8f;

        /// <summary>
        /// How fast a figure moves, from its averaged horizontal <paramref name="speed"/>. A walk or run ends only below
        /// a lower speed than it began at, and a motion held under <see cref="MoveHold"/> seconds stays.
        /// </summary>
        public readonly FigureState Moving(float speed, FigureState current, float held)
        {
            var wanted = Wanted(speed, current);
            var living = current is FigureState.Idle or FigureState.Walk or FigureState.Run;
            return living && held < MoveHold ? current : wanted;
        }

        private readonly FigureState Wanted(float speed, FigureState current)
        {
            var runFrom = current == FigureState.Run ? RunSpeed * SlowShare : RunSpeed;
            if (speed >= runFrom)
            {
                return FigureState.Run;
            }

            var walkFrom = current is FigureState.Walk or FigureState.Run ? WalkSpeed * StopShare : WalkSpeed;
            return speed >= walkFrom ? FigureState.Walk : FigureState.Idle;
        }
    }

    /// <summary>A renderer under one of a squad's figure models that plays its clips (body levels of detail, weapon, material children), of a squad; baked.</summary>
    [InternalBufferCapacity(4)]
    public struct FigureRenderer : IBufferElementData
    {
        /// <summary>The figure model, base or other, this part plays for.</summary>
        public Entity Model;

        public Entity Part;
    }

    /// <summary>What a squad figure shows. Unset is a baked figure the system has not placed yet; the living states are its motion.</summary>
    public enum FigureState : byte
    {
        Unset,
        Idle,
        Walk,
        Run,
        Die,
        Hidden,
    }
}
