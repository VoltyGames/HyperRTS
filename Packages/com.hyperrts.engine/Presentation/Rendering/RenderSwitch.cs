using HyperRTS.Simulation.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Sets one <see cref="HideReason"/> on a gameplay root's meshes and draws each mesh only while it has none, through
    /// <see cref="MaterialMeshInfo"/>'s enable bit, so hiding never moves chunks or leaves zero-scale matrices.
    /// </summary>
    public struct RenderSwitch
    {
        [ReadOnly] private BufferLookup<LinkedEntityGroup> _linked;
        [ReadOnly] private BufferLookup<Child> _children;
        [ReadOnly] private ComponentLookup<Parent> _parents;
        private ComponentLookup<HiddenBy> _hidden;
        private ComponentLookup<MaterialMeshInfo> _meshes;

        public RenderSwitch(ref SystemState state)
        {
            _linked = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            _parents = state.GetComponentLookup<Parent>(true);
            _hidden = state.GetComponentLookup<HiddenBy>();
            _meshes = state.GetComponentLookup<MaterialMeshInfo>();
        }

        public void Update(ref SystemState state)
        {
            _linked.Update(ref state);
            _children.Update(ref state);
            _parents.Update(ref state);
            _hidden.Update(ref state);
            _meshes.Update(ref state);
        }

        public bool IsHidden(Entity entity, HideReason reason) => _hidden[entity].Has(reason);

        /// <summary>Sets <paramref name="reason"/> on <paramref name="root"/> and every part of it.</summary>
        public void SetGroup(Entity root, HideReason reason, bool hide)
        {
            var parts = new NativeList<Entity>(Allocator.Temp);
            RenderHierarchy.Collect(root, _linked, _children, parts);
            foreach (var part in parts)
            {
                Set(part, reason, hide);
            }
        }

        /// <summary>Fills <paramref name="parts"/> with <paramref name="root"/>'s parts, for several branch switches.</summary>
        public void Collect(Entity root, NativeList<Entity> parts) => RenderHierarchy.Collect(root, _linked, _children, parts);

        /// <summary>Sets <paramref name="reason"/> on <paramref name="branch"/> and the parts of <paramref name="root"/> under it.</summary>
        // Walks the root's group, since a fresh instance's branch has no Child buffer until the transform systems run.
        public void SetBranch(Entity root, Entity branch, HideReason reason, bool hide)
        {
            var parts = new NativeList<Entity>(Allocator.Temp);
            Collect(root, parts);
            SetBranch(parts, branch, reason, hide);
        }

        /// <summary>Sets <paramref name="reason"/> on <paramref name="branch"/> and the collected parts under it.</summary>
        public void SetBranch(NativeList<Entity> parts, Entity branch, HideReason reason, bool hide)
        {
            foreach (var part in parts)
            {
                if (IsUnder(part, branch))
                {
                    Set(part, reason, hide);
                }
            }
        }

        /// <summary>Shows <paramref name="root"/>'s stage <paramref name="index"/> and hides its other stages.</summary>
        public void ShowStage<T>(Entity root, NativeArray<T> stages, int index) where T : unmanaged, IModelStage
        {
            var parts = new NativeList<Entity>(Allocator.Temp);
            Collect(root, parts);
            for (var i = 0; i < stages.Length; i++)
            {
                SetBranch(parts, stages[i].Visual, HideReason.Stage, i != index);
            }
        }

        private bool IsUnder(Entity part, Entity branch)
        {
            while (part != branch)
            {
                if (!_parents.TryGetComponent(part, out var parent))
                {
                    return false;
                }

                part = parent.Value;
            }

            return true;
        }

        // Baking gives every mesh and root a HiddenBy, so parts without one draw nothing.
        private void Set(Entity part, HideReason reason, bool hide)
        {
            if (!_hidden.TryGetComponent(part, out var hidden))
            {
                return;
            }

            hidden.Set(reason, hide);
            _hidden[part] = hidden;
            if (_meshes.HasComponent(part))
            {
                _meshes.SetComponentEnabled(part, hidden.Reasons == 0);
            }
        }
    }
}
