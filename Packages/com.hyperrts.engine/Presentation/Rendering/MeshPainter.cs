using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Puts one material property <typeparamref name="T"/> on every mesh under a gameplay root and writes it later, so
    /// per-instance shader values reach a building's child meshes.
    /// </summary>
    public struct MeshPainter<T> where T : unmanaged, IComponentData
    {
        [ReadOnly] private BufferLookup<LinkedEntityGroup> _linked;
        [ReadOnly] private BufferLookup<Child> _children;
        [ReadOnly] private ComponentLookup<MaterialMeshInfo> _meshes;
        private ComponentLookup<T> _painted;

        public MeshPainter(ref SystemState state)
        {
            _linked = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            _meshes = state.GetComponentLookup<MaterialMeshInfo>(true);
            _painted = state.GetComponentLookup<T>();
        }

        /// <summary>Call each frame and again after playing back <see cref="Add"/>'s commands.</summary>
        public void Update(ref SystemState state)
        {
            _linked.Update(ref state);
            _children.Update(ref state);
            _meshes.Update(ref state);
            _painted.Update(ref state);
        }

        public void Collect(Entity root, NativeList<Entity> parts) => RenderHierarchy.Collect(root, _linked, _children, parts);

        public void Add(NativeList<Entity> parts, T value, EntityCommandBuffer commands)
        {
            foreach (var part in parts)
            {
                if (_meshes.HasComponent(part))
                {
                    commands.AddComponent(part, value);
                }
            }
        }

        public void Remove(NativeList<Entity> parts, EntityCommandBuffer commands)
        {
            foreach (var part in parts)
            {
                commands.RemoveComponent<T>(part);
            }
        }

        public void Set(NativeList<Entity> parts, T value)
        {
            foreach (var part in parts)
            {
                if (_painted.HasComponent(part))
                {
                    _painted[part] = value;
                }
            }
        }
    }
}
