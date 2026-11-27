namespace HyperRTS.Simulation.Vision;

/// <summary>The height a target is seen at: low cover hides ground targets, and only range limits aircraft.</summary>
public enum SightLayer : byte
{
    Ground,

    /// <summary>A <see cref="TallTarget"/>, seen over walls and hedges that hide infantry.</summary>
    Tall,

    /// <summary>An aircraft, seen anywhere inside a viewer's range over terrain, woods and crops.</summary>
    Air,
}
