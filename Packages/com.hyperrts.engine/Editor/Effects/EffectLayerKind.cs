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

        /// <summary>Lit mesh chunks that bounce on the ground plane at the burst.</summary>
        Debris,

        /// <summary>Additive quads stretched along their velocity.</summary>
        Spark,
    }
}
