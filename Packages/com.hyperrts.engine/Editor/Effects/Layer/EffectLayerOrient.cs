namespace HyperRTS.Editor.Effects.Layer;

/// <summary>Which way a layer's quads face. Sparks always turn along their velocity, and meshes ignore it.</summary>
public enum EffectLayerOrient
{
    /// <summary>Toward the camera plane, so smoke stays round as the camera turns.</summary>
    Camera,

    /// <summary>Toward the camera, turning only about world up, as a column of water stands.</summary>
    Upright,

    /// <summary>Lying on the ground facing up, as a blast's ring does. A flat puff is a decal projected onto the ground.</summary>
    Flat,
}
