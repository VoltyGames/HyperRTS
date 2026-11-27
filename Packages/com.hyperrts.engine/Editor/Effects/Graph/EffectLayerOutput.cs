using HyperRTS.Editor.Effects.Layer;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>
/// Writes a layer's output context for its kind, then size, colour and frame over life, and the fog of war last.
/// </summary>
public sealed class EffectLayerOutput(GraphModel graph, EffectLayer layer, Vector2 at)
{
    public const string DistortShader = "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/EffectDistort.shadergraph";
    public const string SixWayShader = "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/EffectSixWay.shadergraph";

    // Before the other transparents, which the opaque texture lacks, so smoke and flames are never bent away.
    private const int DistortQueueOffset = -50;

    // Motion maps hold each texel's move to the next frame as plus or minus 1/64 of the sheet (FlipbookMotion.hlsl).
    private const float MotionSheetShare = 1f / 64f;

    private readonly EffectLayer _layer = layer;
    private readonly Vector2 _at = at;

    public void Write(object update)
    {
        var output = Output();
        graph.Flow(update, output);
        DrawKind(output);
        Orient(output);

        if (_layer.softness > 0f && !_layer.DrawsShaderGraph && !_layer.Projected)
        {
            graph.Set(output, "useSoftParticle", true);
            graph.Slot(output, "softParticleFadeDistance", _layer.softness);
        }

        OverLife(output);
        if (!_layer.DrawsShaderGraph)
        {
            var fog = graph.Hlsl(output, "Fog Of War", EffectLayerParticles.Blocks + "ShadeInFogOfWar.hlsl");
            graph.Slot(fog, "glow", _layer.Glows ? 1f : 0f);
        }
    }

    private void DrawKind(object output)
    {
        switch (_layer.kind)
        {
            case EffectLayerKind.Glow:
            case EffectLayerKind.Spark:
                Glow(output);
                break;
            case EffectLayerKind.Puff:
                Puff(output);
                break;
            case EffectLayerKind.SixWay:
            case EffectLayerKind.Ring:
                SixWay(output);
                break;
            case EffectLayerKind.Debris:
                Debris(output);
                break;
            case EffectLayerKind.Distort:
                Distort(output);
                break;
        }
    }

    private object Output() => _layer.kind switch
    {
        EffectLayerKind.Distort => graph.ShaderGraphOutput(DistortShader, DistortQueueOffset, _at),
        EffectLayerKind.SixWay or EffectLayerKind.Ring => graph.ShaderGraphOutput(SixWayShader, 0, _at),
        _ => graph.Context(Context(), _at),
    };

    private string Context() => _layer.kind switch
    {
        EffectLayerKind.Glow or EffectLayerKind.Spark => "VFXPlanarPrimitiveOutput",
        EffectLayerKind.Debris => "VFXURPLitMeshOutput",
        EffectLayerKind.Puff when _layer.Projected => "VFXDecalURPOutput",
        _ => "VFXURPLitPlanarPrimitiveOutput",
    };

    private void Glow(object output)
    {
        graph.Set(output, "blendMode", "Additive");
        Flipbook(output);
        graph.Slot(output, "mainTexture", _layer.texture);
    }

    // Unshadowed: a puff at ground level would take the grass's shadows. A decal tints the ground and no more.
    private void Puff(object output)
    {
        if (_layer.Projected)
        {
            graph.Set(output, "affectMAOS", false);
            graph.Set(output, "angleFade", false);
        }
        else
        {
            graph.Set(output, "blendMode", "Alpha");
            graph.Set(output, "receiveShadows", false);
            graph.Set(output, "useBaseColorMap", "ColorAndAlpha");
        }

        Flipbook(output);
        graph.Slot(output, "baseColorMap", _layer.texture);
    }

    // The Shader Graph lights, fogs and glows from the stored colour, alpha and frame.
    private void SixWay(object output)
    {
        graph.Slot(output, "PositiveMap", _layer.texture);
        graph.Slot(output, "NegativeMap", _layer.sixWay.negativeTexture);
        graph.Slot(output, "MotionMap", _layer.motionTexture);
        graph.Slot(output, "Opacity", _layer.sixWay.opacity);
        graph.Slot(output, "AmbientStrength", _layer.sixWay.ambient);
        graph.Slot(output, "SoftDistance", _layer.softness);
        if (_layer.sixWay.emissionRamp != null)
        {
            Heat(output);
        }

        if (_layer.flipbook.IsSet)
        {
            graph.Slot(output, "FlipbookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
            LinkAttribute("texIndex", output, "Frame", 400f);
        }

        LinkAttribute("color", output, "ParticleColor", 300f);
        LinkAttribute("alpha", output, "ParticleAlpha", 350f);
    }

    // The emission ramp at the emission channel times the heat over life, times the emission colour.
    private void Heat(object output)
    {
        // HDR emission as the colour picker stores it, not turned linear, matched in Play.
        var color = _layer.sixWay.emissionColor;
        graph.Slot(output, "EmissionRamp", _layer.sixWay.emissionRamp);
        graph.Slot(output, "EmissionColor", new Vector3(color.r, color.g, color.b));
        var age = graph.Operator("AgeOverLifetime", _at + new Vector2(-780f, 200f));
        var heat = HeatAtAge(_layer.sixWay.heatOverLife, age, 200f);
        if (_layer.sixWay.minHeatOverLife.length > 0)
        {
            // Each particle keeps one pick between the curves, so tongues differ in heat.
            var pick = graph.Operator("UnityEditor.VFX.Operator.Lerp", _at + new Vector2(-300f, 200f));
            graph.Link(HeatAtAge(_layer.sixWay.minHeatOverLife, age, 300f), pick, "x");
            graph.Link(heat, pick, "y");
            graph.Link(graph.Operator("UnityEditor.VFX.Operator.Random", _at + new Vector2(-520f, 400f)), pick, "s");
            heat = pick;
        }

        graph.Link(heat, output, "Heat");
    }

    private object HeatAtAge(AnimationCurve curve, object age, float y)
    {
        var heat = graph.Operator("SampleCurve", _at + new Vector2(-520f, y));
        graph.Slot(heat, "curve", curve);
        graph.Link(age, heat, "time");
        return heat;
    }

    private void Debris(object output)
    {
        // Chunks draw with their colour, tinted by the texture when set; a base colour map left empty reads as clear.
        var textured = _layer.texture != null;
        graph.Set(output, "blendMode", "Opaque");
        graph.Set(output, "useBaseColorMap", textured ? "Color" : "None");
        if (textured)
        {
            graph.Slot(output, "baseColorMap", _layer.texture);
        }

        graph.Set(output, "castShadows", true);
        var meshes = _layer.debris.meshes;
        graph.Set(output, "MeshCount", (uint)meshes.Count);
        if (meshes.Count == 1)
        {
            graph.Slot(output, "mesh", meshes[0]);
        }
        else
        {
            for (var i = 0; i < meshes.Count; i++)
            {
                graph.Slot(output, "mesh" + i, meshes[i]);
            }
        }

        graph.Slot(output, "metallic", _layer.debris.metallic);
        graph.Slot(output, "smoothness", _layer.debris.smoothness);
    }

    // The Shader Graph reads the stored alpha as its bend and does the fog of war per pixel.
    private void Distort(object output)
    {
        graph.Slot(output, "NormalMap", _layer.texture);
        graph.Slot(output, "Strength", _layer.distort.distortion);
        graph.Slot(output, "Scroll", _layer.distort.scroll);
        graph.Slot(output, "SoftFade", _layer.softness);
        LinkAttribute("alpha", output, "Weight", 200f);
    }

    // Without an Orient block a quad keeps VFX Graph's world axes and stands edge-on once the camera turns.
    private void Orient(object output)
    {
        var mode = _layer.OrientMode;
        if (mode == null)
        {
            return;
        }

        var orient = graph.Block(output, "Orient");
        graph.Set(orient, "mode", mode);
        switch (mode)
        {
            case "FixedAxis":
                graph.Slot(orient, "Up", Vector3.up);
                break;
            case "Advanced":
                graph.Set(orient, "axes", "ZY");
                graph.Slot(orient, "AxisZ", Vector3.up);
                graph.Slot(orient, "AxisY", Vector3.forward);
                break;
        }
    }

    private void LinkAttribute(string attribute, object output, string input, float y)
    {
        var read = graph.Operator("VFXAttributeParameter", _at + new Vector2(-300f, y));
        graph.Set(read, "attribute", attribute);
        graph.Link(read, output, input);
    }

    private void Flipbook(object output)
    {
        if (!_layer.flipbook.IsSet)
        {
            return;
        }

        var motion = _layer.motionTexture != null;
        graph.Set(output, "uvMode", "Flipbook");
        graph.Set(output, "flipbookBlendFrames", !_layer.flipbook.random);
        graph.Set(output, "flipbookMotionVectors", motion);
        graph.Slot(output, "flipBookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
        if (motion)
        {
            graph.Slot(output, "motionVectorMap", _layer.motionTexture);
            graph.Slot(output, "motionVectorScale", _layer.flipbook.columns * MotionSheetShare);
        }
    }

    private void OverLife(object output)
    {
        if (_layer.sizeOverLife.length > 0)
        {
            Curve(output, "size", _layer.sizeOverLife, "Multiply");
        }

        if (_layer.alphaOverLife.length > 0 && !_layer.KeepsAlpha)
        {
            Curve(output, "color", _layer.AlphaGradient(), "Multiply");
        }

        if (_layer.flipbook.PlaysOverLife && !_layer.DrawsShaderGraph)
        {
            Curve(output, "texIndex", _layer.FrameCurve(), "Overwrite");
        }

        if (_layer.kind == EffectLayerKind.Spark)
        {
            var stretch = graph.Hlsl(output, "Stretch Along Velocity", EffectLayerParticles.Blocks + "StretchAlongVelocity.hlsl");
            graph.Slot(stretch, "sizeLength", _layer.stretch.length);
            graph.Slot(stretch, "speedLength", _layer.stretch.lengthPerSpeed);
        }
    }

    private void Curve(object output, string attribute, object curve, string composition)
    {
        var block = graph.Block(output, "AttributeFromCurve");
        graph.Set(block, "attribute", attribute);
        graph.Set(block, "Composition", composition);
        if (attribute == "color")
        {
            graph.Set(block, "AlphaComposition", composition);
        }

        graph.SlotAt(block, 0, curve);
    }
}
