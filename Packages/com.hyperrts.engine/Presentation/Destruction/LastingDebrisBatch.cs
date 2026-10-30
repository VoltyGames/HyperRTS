using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The lasting debris pieces sharing one mesh and material: the slots it draws, as the GPU reads them, and its
    /// indirect draw command.
    /// </summary>
    internal sealed class LastingDebrisBatch
    {
        private readonly uint[] _slots;

        public LastingDebrisBatch(Mesh mesh, Material material, BatchMeshID meshID, BatchMaterialID materialID, int capacity)
        {
            Mesh = mesh;
            Material = material;
            MeshID = meshID;
            MaterialID = materialID;
            _slots = new uint[capacity];
        }

        public Mesh Mesh { get; }

        public Material Material { get; }

        public BatchMeshID MeshID { get; }

        public BatchMaterialID MaterialID { get; }

        public int Count { get; private set; }

        /// <summary>Its slot list changed since the GPU copy was last written.</summary>
        public bool Changed { get; set; }

        public uint[] Slots => _slots;

        public GraphicsBuffer.IndirectDrawIndexedArgs Command => new()
        {
            indexCountPerInstance = Mesh.GetIndexCount(0),
            instanceCount = (uint)Count,
            startIndex = Mesh.GetIndexStart(0),
            baseVertexIndex = Mesh.GetBaseVertex(0),
        };

        public bool Draws(Mesh mesh, Material material) => Mesh == mesh && Material == material;

        /// <summary>Appends <paramref name="slot"/> and returns where it sits in the list.</summary>
        public int Add(int slot)
        {
            _slots[Count] = (uint)slot;
            Changed = true;
            return Count++;
        }

        /// <summary>Removes the slot at <paramref name="index"/> by moving the last one there, and returns the moved slot.</summary>
        public int Remove(int index)
        {
            var last = _slots[--Count];
            _slots[index] = last;
            Changed = true;
            return (int)last;
        }
    }
}
