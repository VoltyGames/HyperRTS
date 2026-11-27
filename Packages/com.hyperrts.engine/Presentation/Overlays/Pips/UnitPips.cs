using UnityEngine;

namespace HyperRTS.Presentation.Overlays.Pips;

/// <summary>One frame's owner-coloured pips over units too small on screen to pick out, all in one draw.</summary>
public sealed class UnitPips(Mesh billboard, Material material) : OverlayBatch<OverlayInstance>(billboard, material)
{
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int BackId = Shader.PropertyToID("_Back");

    /// <summary>Whether a unit <paramref name="screenHeight"/> reference pixels tall is too small to pick out.</summary>
    public static bool Shows(float screenHeight, OverlayStyle style) => screenHeight < style.smallUnitHeight;

    public void Add(Vector3 top, Color owner) => Add(new OverlayInstance(Matrix4x4.Translate(top), owner));

    protected override void ApplyStyle(OverlayStyle style, Material material)
    {
        material.SetFloat(SizeId, style.unitPipSize);
        material.SetFloat(OffsetId, style.unitPipOffset);
        material.SetColor(BackId, style.markerBack);
    }
}
