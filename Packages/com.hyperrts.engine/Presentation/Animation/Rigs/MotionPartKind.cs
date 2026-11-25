namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>How a <see cref="MotionRigPart"/> moves.</summary>
    public enum MotionPartKind : byte
    {
        Turret = 0,
        Elevation = 1,
        Recoil = 2,
        Wheel = 3,
        Rotor = 4,
        SteeringWheel = 5,
        Trailer = 6,
    }
}
