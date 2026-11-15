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
            Values(Block(context, attribute), range.IsConstant, range.min, range.max);
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
            var block = Block(context, attribute);
            _graph.Set(block, "channels", channel);
            Values(block, range.IsConstant, range.min, range.max);
        }

        public void PickPerAxis(object context, string attribute, FloatRange range)
        {
            var block = Block(context, attribute);
            _graph.Set(block, "Random", "PerComponent");
            _graph.Slot(block, "A", Vector3.one * range.min);
            _graph.Slot(block, "B", Vector3.one * range.max);
        }

        public void PickColor(object context, ColorRange range)
        {
            var block = Block(context, "color");
            var min = range.min.linear;
            var max = range.max.linear;
            Values(block, min == max, new Vector3(min.r, min.g, min.b), new Vector3(max.r, max.g, max.b));
        }

        // Truncating a uniform pick over 0 to count gives each index an even share.
        public void PickIndex(object context, string attribute, int count)
        {
            Values(Block(context, attribute), false, 0u, (uint)count);
        }

        private object Block(object context, string attribute)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            return block;
        }

        private void Values(object block, bool constant, object min, object max)
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
