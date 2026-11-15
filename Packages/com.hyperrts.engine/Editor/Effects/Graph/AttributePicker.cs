using HyperRTS.Editor.Effects.Layer;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>Writes the Set Attribute blocks that pick a value per particle, in VFX Graph's own terms.</summary>
    public sealed class AttributePicker
    {
        private readonly GraphModel _graph;

        public AttributePicker(GraphModel graph)
        {
            _graph = graph;
        }

        public void Pick(object context, string attribute, FloatRange range)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            Values(block, range.IsConstant, range.min, range.max);
        }

        // A planar particle turns about the view axis alone.
        public void PickAngle(object context, string attribute, FloatRange range)
        {
            if (range.IsConstant && range.min == 0f)
            {
                return;
            }

            PickChannel(context, attribute, "Z", range);
        }

        // One channel of a vector attribute, as VFX Graph stores it (scale Y, not a scaleY attribute it must upgrade).
        public void PickChannel(object context, string attribute, string channel, FloatRange range)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            _graph.Set(block, "channels", channel);
            Values(block, range.IsConstant, range.min, range.max);
        }

        public void PickPerAxis(object context, string attribute, FloatRange range)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            _graph.Set(block, "Random", "PerComponent");
            _graph.Slot(block, "A", Vector3.one * range.min);
            _graph.Slot(block, "B", Vector3.one * range.max);
        }

        public void Values(object block, bool constant, object min, object max)
        {
            if (constant)
            {
                _graph.SlotAt(block, 0, min);
                return;
            }

            _graph.Set(block, "Random", "Uniform");
            _graph.Slot(block, "A", min);
            _graph.Slot(block, "B", max);
        }
    }
}
