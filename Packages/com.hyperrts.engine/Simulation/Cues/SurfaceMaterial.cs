namespace HyperRTS.Simulation.Cues
{
    /// <summary>What a surface is made of, so hits and wheels throw up the matching debris.</summary>
    public enum SurfaceMaterial : byte
    {
        None = 0,
        Soil = 1,
        Grass = 2,
        Mud = 3,
        Sand = 4,
        Gravel = 5,
        Paved = 6,
        Snow = 7,
        Water = 8,
        Wood = 9,
        Metal = 10,
        Masonry = 11,
        Concrete = 12,
        Foliage = 13,
    }
}
