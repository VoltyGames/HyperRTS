namespace HyperRTS.Editor.Effects
{
    /// <summary>What a layer draws; each kind is one VFX Graph output.</summary>
    public enum EffectLayerKind
    {
        /// <summary>Additive unlit quads, such as a flash.</summary>
        Glow,

        /// <summary>Lit alpha-blended flipbook quads, smoke or dust.</summary>
        Puff,

        /// <summary>Six-way lit flipbook smoke that may glow from inside.</summary>
        SixWay,

        /// <summary>Six-way smoke thrown outward along the ground from a disc.</summary>
        Ring,

        /// <summary>Lit mesh chunks that fall, bouncing on the ground plane at the burst when the layer has ground.</summary>
        Debris,

        /// <summary>Additive quads stretched along their velocity.</summary>
        Spark,

        /// <summary>A lit ribbon through a stream's particles, such as a missile's smoke trail.</summary>
        Strip,

        /// <summary>Quads that bend the scene behind them along a normal map, through HyperRTS/EffectDistort.</summary>
        Distort,

        /// <summary>Invisible particles that emit their child layers over their life.</summary>
        Source,
    }
}
