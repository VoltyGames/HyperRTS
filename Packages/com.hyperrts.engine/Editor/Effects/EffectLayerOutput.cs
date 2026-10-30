using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes a layer's output context for its kind, then the per-frame looks every kind shares: size, colour and
    /// flipbook frame over life, and the fog of war last.
    /// </summary>
    public sealed class EffectLayerOutput
    {
        public const string DistortShader = "Packages/com.hyperrts.engine/Presentation/Effects/EffectDistort.shadergraph";
        public const string SixWayShader = "Packages/com.hyperrts.engine/Presentation/Effects/EffectSixWay.shadergraph";

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
            if (_layer.DrawsShaderGraph && _layer.colorOverLife.Count > 0)
            {
                throw new InvalidOperationException($"HyperRTS: {_layer.kind} layer '{_layer.name}' draws through a Shader Graph, which reads the start colour; it takes no colorOverLife.");
            }

            var output = Output();
            _graph.Flow(update, output);
            switch (_layer.Kind)
            {
                case EffectLayerKind.Glow:
                    Glow(output);
                    break;
                case EffectLayerKind.Spark:
                    var orient = _graph.Block(output, "Orient");
                    _graph.Set(orient, "mode", "AlongVelocity");
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
                case EffectLayerKind.Strip:
                    Strip(output);
                    break;
                case EffectLayerKind.Distort:
                    Distort(output);
                    break;
            }

            if (_layer.upright)
            {
                var upright = _graph.Block(output, "Orient");
                _graph.Set(upright, "mode", "FixedAxis");
                _graph.Slot(upright, "Up", Vector3.up);
            }

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

            var fog = _graph.Hlsl(output, "Fog Of War", EffectLayerSystem.Includes + "GraphFogOfWar.hlsl");
            _graph.Slot(fog, "glow", _layer.Glows ? 1f : 0f);
        }

        private object Output() => _layer.Kind switch
        {
            EffectLayerKind.Distort => _graph.ShaderGraphOutput(DistortShader, DistortQueueOffset, _at),
            EffectLayerKind.SixWay or EffectLayerKind.Ring => _graph.ShaderGraphOutput(SixWayShader, 0, _at),
            _ => _graph.Context(Context(), _at),
        };

        private string Context() => _layer.Kind switch
        {
            EffectLayerKind.Glow or EffectLayerKind.Spark => "VFXPlanarPrimitiveOutput",
            EffectLayerKind.Debris => "VFXURPLitMeshOutput",
            EffectLayerKind.Strip => "VFXURPLitQuadStripOutput",
            _ => "VFXURPLitPlanarPrimitiveOutput",
        };

        private void Glow(object output)
        {
            _graph.Set(output, "blendMode", "Additive");
            Flipbook(output);
            _graph.Slot(output, "mainTexture", _layer.Load<Texture2D>(_layer.texture, "texture"));
        }

        private void Puff(object output)
        {
            _graph.Set(output, "blendMode", "Alpha");
            _graph.Set(output, "useBaseColorMap", "ColorAndAlpha");
            Flipbook(output);
            _graph.Slot(output, "baseColorMap", _layer.Load<Texture2D>(_layer.texture, "texture"));
        }

        // The Shader Graph lights, fogs and glows as ParticlesSixWay does, from the stored colour, alpha and frame.
        private void SixWay(object output)
        {
            if (_layer.opacity <= 0f || _layer.ambient <= 0f)
            {
                throw new InvalidOperationException($"HyperRTS: {_layer.kind} layer '{_layer.name}' needs an opacity and an ambient, as its six-way material's Density and Ambient Strength.");
            }

            _graph.Slot(output, "PositiveMap", _layer.Load<Texture2D>(_layer.texture, "texture"));
            _graph.Slot(output, "NegativeMap", _layer.Load<Texture2D>(_layer.negativeTexture, "negativeTexture"));
            _graph.Slot(output, "MotionMap", _layer.Load<Texture2D>(_layer.motionTexture, "motionTexture"));
            _graph.Slot(output, "Opacity", _layer.opacity);
            _graph.Slot(output, "AmbientStrength", _layer.ambient);
            _graph.Slot(output, "SoftDistance", _layer.softness);
            if (!string.IsNullOrEmpty(_layer.emissionRamp))
            {
                SixWayGlow(output);
            }

            if (_layer.flipbook.IsSet)
            {
                _graph.Slot(output, "FlipbookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
                LinkAttribute("texIndex", output, "Frame", 400f);
            }

            LinkAttribute("color", output, "ParticleColor", 300f);
            LinkAttribute("alpha", output, "ParticleAlpha", 350f);
        }

        // The ramp at the emission channel times the heat over life, times the emission colour (M_FX_SixWay_Fire).
        private void SixWayGlow(object output)
        {
            // As the material stores it: HDR emission reaches ParticlesSixWay unconverted, matched in Play.
            var color = _layer.emissionColor;
            _graph.Slot(output, "EmissionRamp", _layer.Load<Texture2D>(_layer.emissionRamp, "emissionRamp"));
            _graph.Slot(output, "EmissionColor", new Vector3(color.r, color.g, color.b));
            var heat = _graph.Operator("SampleCurve", _at + new Vector2(-300f, 200f));
            _graph.Slot(heat, "curve", _layer.HeatCurve());
            _graph.Link(_graph.Operator("AgeOverLifetime", _at + new Vector2(-520f, 200f)), heat, "time");
            _graph.Link(heat, output, "Heat");
        }

        private void Debris(object output)
        {
            // Chunks draw with their colour alone; a base colour map left empty reads as clear.
            _graph.Set(output, "blendMode", "Opaque");
            _graph.Set(output, "useBaseColorMap", "None");
            _graph.Set(output, "castShadows", true);
            var meshes = _layer.Meshes();
            _graph.Set(output, "MeshCount", (uint)meshes.Length);
            if (meshes.Length == 1)
            {
                _graph.Slot(output, "mesh", meshes[0]);
            }
            else
            {
                for (var i = 0; i < meshes.Length; i++)
                {
                    _graph.Slot(output, "mesh" + i, meshes[i]);
                }
            }

            _graph.Slot(output, "metallic", _layer.metallic);
            _graph.Slot(output, "smoothness", _layer.smoothness);
        }

        private void Strip(object output)
        {
            _graph.Set(output, "blendMode", "Alpha");
            _graph.Set(output, "useBaseColorMap", "ColorAndAlpha");
            _graph.Slot(output, "baseColorMap", _layer.Load<Texture2D>(_layer.texture, "texture"));
        }

        // The Shader Graph reads the stored alpha as its bend and does the fog of war per pixel.
        private void Distort(object output)
        {
            _graph.Slot(output, "NormalMap", _layer.Load<Texture2D>(_layer.texture, "texture"));
            _graph.Slot(output, "Strength", _layer.distortion);
            _graph.Slot(output, "Scroll", _layer.scroll);
            _graph.Slot(output, "SoftFade", _layer.softness);
            LinkAttribute("alpha", output, "Weight", 200f);
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

            var motion = !string.IsNullOrEmpty(_layer.motionTexture);
            _graph.Set(output, "uvMode", "Flipbook");
            _graph.Set(output, "flipbookBlendFrames", !_layer.flipbook.random);
            _graph.Set(output, "flipbookMotionVectors", motion);
            _graph.Slot(output, "flipBookSize", new Vector2(_layer.flipbook.columns, _layer.flipbook.rows));
            if (motion)
            {
                _graph.Slot(output, "motionVectorMap", _layer.Load<Texture2D>(_layer.motionTexture, "motionTexture"));
                _graph.Slot(output, "motionVectorScale", _layer.flipbook.columns * MotionSheetShare);
            }
        }

        private void OverLife(object output)
        {
            if (_layer.sizeOverLife.Count > 0)
            {
                Curve(output, "size", _layer.SizeCurve(), "Multiply");
            }

            if (_layer.colorOverLife.Count > 0 || (_layer.alphaOverLife.Count > 0 && !_layer.KeepsAlpha))
            {
                Curve(output, "color", _layer.LifeGradient(), "Multiply");
            }

            if (_layer.flipbook.IsSet && !_layer.flipbook.random && !_layer.DrawsShaderGraph)
            {
                Curve(output, "texIndex", _layer.FrameCurve(), "Overwrite");
            }

            if (_layer.Kind == EffectLayerKind.Spark)
            {
                var stretch = _graph.Hlsl(output, "Stretch Along Velocity", EffectLayerSystem.Includes + "GraphStretch.hlsl");
                _graph.Slot(stretch, "sizeLength", _layer.length);
                _graph.Slot(stretch, "speedLength", _layer.lengthPerSpeed);
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
