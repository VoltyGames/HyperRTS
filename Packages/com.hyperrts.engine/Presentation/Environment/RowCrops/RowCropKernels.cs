using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>The RowCrops compute shader and its two kernels: Plant grows plants, Count turns them into draw args.</summary>
    internal readonly struct RowCropKernels
    {
        public readonly ComputeShader Compute;
        public readonly int Plant;
        public readonly int Count;

        public RowCropKernels(ComputeShader compute)
        {
            Compute = compute;
            Plant = compute.FindKernel(nameof(Plant));
            Count = compute.FindKernel(nameof(Count));
        }
    }
}
