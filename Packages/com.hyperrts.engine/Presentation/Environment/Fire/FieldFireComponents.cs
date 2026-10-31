using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Fire
{
    /// <summary>The enabled <see cref="FieldFireSettings"/>, as a singleton; absent means no fires.</summary>
    public struct FieldFireSource : IComponentData
    {
        public UnityObjectRef<FieldFireSettings> Settings;
    }

    /// <summary>What <see cref="FieldFireSystem"/> reports to the plants, as a singleton it creates.</summary>
    public struct FieldFireState : IComponentData
    {
        /// <summary>Cells burnt out so far, so a still camera's grass regrows once ash covers more ground.</summary>
        public int BurntCells;
    }
}
