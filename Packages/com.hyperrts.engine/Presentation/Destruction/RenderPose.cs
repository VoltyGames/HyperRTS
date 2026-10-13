using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows an entity turned away from its simulation rotation by writing only <see cref="LocalToWorld"/> for it and
    /// its children, after the transform systems ran, so the simulation transform stays untouched.
    /// </summary>
    public struct RenderPose
    {
        private ComponentLookup<LocalTransform> _locals;
        private ComponentLookup<PostTransformMatrix> _stretches;
        private ComponentLookup<LocalToWorld> _worlds;
        private BufferLookup<Child> _children;

        public RenderPose(ref SystemState state)
        {
            _locals = state.GetComponentLookup<LocalTransform>(true);
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
            _worlds = state.GetComponentLookup<LocalToWorld>();
            _children = state.GetBufferLookup<Child>(true);
        }

        public void Update(ref SystemState state)
        {
            _locals.Update(ref state);
            _stretches.Update(ref state);
            _worlds.Update(ref state);
            _children.Update(ref state);
        }

        public float3 Position(Entity root) => _locals[root].Position;

        public quaternion Rotation(Entity root) => _locals[root].Rotation;

        /// <summary>Same matrices as the transform systems, with <paramref name="rotation"/> on the root.</summary>
        public void Show(Entity root, quaternion rotation) =>
            Place(root, _locals[root].WithRotation(rotation).ToMatrix());

        private void Place(Entity entity, float4x4 matrix)
        {
            if (_stretches.TryGetComponent(entity, out var stretch))
            {
                matrix = math.mul(matrix, stretch.Value);
            }

            _worlds[entity] = new LocalToWorld { Value = matrix };
            if (!_children.TryGetBuffer(entity, out var children))
            {
                return;
            }

            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i].Value;
                Place(child, math.mul(matrix, _locals[child].ToMatrix()));
            }
        }
    }
}
