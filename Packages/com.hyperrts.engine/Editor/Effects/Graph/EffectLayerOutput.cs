using HyperRTS.Editor.Effects.Layer;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>
    /// Writes a layer's output context for its kind, then the per-frame looks every kind shares: size, colour and
    /// flipbook frame over life, and the fog of war last.
    /// </summary>
    public sealed class EffectLayerOutput
    {
        public const string DistortShader = "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/EffectDistort.shadergraph";
        public const string SixWayShader = "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/EffectSixWay.shadergraph";

        // Before the other transparents, which the opaque texture lacks, so smoke and flames are never bent away.
        private const int DistortQueueOffset = -50;

        // Motion maps hold each texel's move to the next frame as plus or minus 1/64 of the sheet (FlipbookMotion.hlsl).
        private const float MotionSheetShare = 1f / 64f;

        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly Vector2 _at;

        public EffectLayerOutput(GraphModel graph, EffectLayer layer, Vector2 at)
        {
            _graph = graph;
            _layer = layer;
            _at = at;
        }

        public void Write(object update)
        {
            var output = Output();
            _graph.Flow(update, output);
            DrawKind(output);
            Orient(output);

            if (_layer.softness > 0f && !_layer.DrawsShaderGraph)
            {
                _graph.Set(output, "useSoftParticle", true);
                _graph.Slot(output, "softParticleFadeDistance", _layer.softness);
            }

            OverLife(output);
            if (_layer.DrawsShaderGraph)
            {
                return;
            }

            var fog = _graph.Hlsl(output, "Fog Of War", EffectLayerParticles.Includes + "GraphFogOfWar.hlsl");
            _graph.Slot(fog, "glow", _layer.Glows ? 1f : 0f);
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
            EffectLayerKind.Distort => _graph.ShaderGraphOutput(DistortShader, DistortQueueOffset, _at),
            EffectLayerKind.SixWay or EffectLayerKind.Ring => _graph.ShaderGraphOutput(SixWayShader, 0, _at),
            _ => _graph.Context(Context(), _at),
        };

        private string Context() => _layer.kind switch
        {
            EffectLayerKind.Glow or EffectLayerKind.Spark => "VFXPlanarPrimitiveOutput",
            EffectLayerKind.Debris => "VFXURPLitMeshOutput",
            _ => "VFXURPLitPlanarPrimitiveOutput",
        };

        private void Glow(object output)
        {
            _graph.Set(output, "blendMode", "Additive");
            Flipbook(output);
            _graph.Slot(output, "mainTexture", _layer.texture);
        }

        // Unshadowed: a puff at ground level would take the grass's shadows.
        private void Puff(object output)
        {
            _graph.Set(output, "blendMode", "Alpha");
            _graph.Set(output, "receiveShadows", false);
            _graph.Set(output, "useBaseColorMap", "ColorAndAlpha");
            Flipbook(output);
            _graph.Slot(output, "baseColorMap", _layer.texture);
        }

        // The Shader Graph lights, fogs and glows from the stored colour, alpha and frame.
        private void SixWay(object output)
        {
            _graph.Slot(output, "PositiveMap", _layer.texture);
            _graph.Slot(output, "NegativeMap", _layer.sixWay.negativeTexture);
            _graph.Slot(output, "MotionMap", _layer.motionTexture);
            _graph.Slot(output, "Opacity", _layer.sixWay.opacity);
            _graph.Slot(output, "AmbientStrength", _layer.sixWay.ambient);
            _graph.Slot(output, "SoftDistance", _layer.softness);
            if (_layer.sixWay.emissionRamp != null)
            {
                Heat(output);
            }

            if (_layer.flipbook.IsSet)
            {
                _graph.Slot(output, "FlipbookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
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
            _graph.Slot(output, "EmissionRamp", _layer.sixWay.emissionRamp);
            _graph.Slot(output, "EmissionColor", new Vector3(color.r, color.g, color.b));
            var age = _graph.Operator("AgeOverLifetime", _at + new Vector2(-780f, 200f));
            var heat = HeatAtAge(_layer.sixWay.heatOverLife, age, 200f);
            if (_layer.sixWay.minHeatOverLife.length > 0)
            {
                // Each particle keeps one pick between the curves, so tongues differ in heat.
                var pick = _graph.Operator("UnityEditor.VFX.Operator.Lerp", _at + new Vector2(-300f, 200f));
                _graph.Link(HeatAtAge(_layer.sixWay.minHeatOverLife, age, 300f), pick, "x");
                _graph.Link(heat, pick, "y");
                _graph.Link(_graph.Operator("UnityEditor.VFX.Operator.Random", _at + new Vector2(-520f, 400f)), pick, "s");
                heat = pick;
            }

            _graph.Link(heat, output, "Heat");
        }

        private object HeatAtAge(AnimationCurve curve, object age, float y)
        {
            var heat = _graph.Operator("SampleCurve", _at + new Vector2(-520f, y));
            _graph.Slot(heat, "curve", curve);
            _graph.Link(age, heat, "time");
            return heat;
        }

        private void Debris(object output)
        {
            // Chunks draw with their colour, tinted by the texture when set; a base colour map left empty reads as clear.
            var textured = _layer.texture != null;
            _graph.Set(output, "blendMode", "Opaque");
            _graph.Set(output, "useBaseColorMap", textured ? "Color" : "None");
            if (textured)
            {
                _graph.Slot(output, "baseColorMap", _layer.texture);
            }

            _graph.Set(output, "castShadows", true);
            var meshes = _layer.debris.meshes;
            _graph.Set(output, "MeshCount", (uint)meshes.Count);
            if (meshes.Count == 1)
            {
                _graph.Slot(output, "mesh", meshes[0]);
            }
            else
            {
                for (var i = 0; i < meshes.Count; i++)
                {
                    _graph.Slot(output, "mesh" + i, meshes[i]);
                }
            }

            _graph.Slot(output, "metallic", _layer.debris.metallic);
            _graph.Slot(output, "smoothness", _layer.debris.smoothness);
        }

        // The Shader Graph reads the stored alpha as its bend and does the fog of war per pixel.
        private void Distort(object output)
        {
            _graph.Slot(output, "NormalMap", _layer.texture);
            _graph.Slot(output, "Strength", _layer.distort.distortion);
            _graph.Slot(output, "Scroll", _layer.distort.scroll);
            _graph.Slot(output, "SoftFade", _layer.softness);
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

            var orient = _graph.Block(output, "Orient");
            _graph.Set(orient, "mode", mode);
            if (mode == "FixedAxis")
            {
                _graph.Slot(orient, "Up", Vector3.up);
            }
        }

        private void LinkAttribute(string attribute, object output, string input, float y)
        {
            var read = _graph.Operator("VFXAttributeParameter", _at + new Vector2(-300f, y));
            _graph.Set(read, "attribute", attribute);
            _graph.Link(read, output, input);
        }

        private void Flipbook(object output)
        {
            if (!_layer.flipbook.IsSet)
            {
                return;
            }

            var motion = _layer.motionTexture != null;
            _graph.Set(output, "uvMode", "Flipbook");
            _graph.Set(output, "flipbookBlendFrames", !_layer.flipbook.random);
            _graph.Set(output, "flipbookMotionVectors", motion);
            _graph.Slot(output, "flipBookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
            if (motion)
            {
                _graph.Slot(output, "motionVectorMap", _layer.motionTexture);
                _graph.Slot(output, "motionVectorScale", _layer.flipbook.columns * MotionSheetShare);
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
                var stretch = _graph.Hlsl(output, "Stretch Along Velocity", EffectLayerParticles.Includes + "GraphStretch.hlsl");
                _graph.Slot(stretch, "sizeLength", _layer.stretch.length);
                _graph.Slot(stretch, "speedLength", _layer.stretch.lengthPerSpeed);
            }
        }

        private void Curve(object output, string attribute, object curve, string composition)
        {
            var block = _graph.Block(output, "AttributeFromCurve");
            _graph.Set(block, "attribute", attribute);
            _graph.Set(block, "Composition", composition);
            if (attribute == "color")
            {
                _graph.Set(block, "AlphaComposition", composition);
            }

            _graph.SlotAt(block, 0, curve);
        }
    }
}
