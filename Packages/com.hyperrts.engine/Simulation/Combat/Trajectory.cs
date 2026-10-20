namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// How a projectile flies: straight and homing, an arc onto a point fixed at launch (artillery), or a steep climb
    /// then a guided dive onto its target (rockets and missiles).
    /// </summary>
    public enum Trajectory : byte
    {
        Direct = 0,
        Ballistic = 1,
        Rocket = 2,
    }
}
