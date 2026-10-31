using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The lookups that find a root's parts, shared by <see cref="RenderSwitch"/> and <see cref="MeshPainter{T}"/>.</summary>
    public struct RenderParts
    {
        [ReadOnly] private BufferLookup<LinkedEntityGroup> _linked;
        [ReadOnly] private BufferLookup<Child> _children;

        public RenderParts(ref SystemState state)
        {
            _linked = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
        }

        public void Update(ref SystemState state)
        {
            _linked.Update(ref state);
            _children.Update(ref state);
        }

        /// <summary>Fills <paramref name="parts"/> with <paramref name="root"/>'s parts.</summary>
        public void Collect(Entity root, NativeList<Entity> parts) => RenderHierarchy.Collect(root, _linked, _children, parts);
    }
}
