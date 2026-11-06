using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>What makes a squad figure show its other model.</summary>
    public enum VariantReason : byte
    {
        /// <summary>Its member fires the upgraded weapon.</summary>
        Upgraded,

        /// <summary>Its squad's weapon is set up and its member takes a crew place.</summary>
        Emplaced,
    }

    /// <summary>What a <see cref="SquadFigure"/> keeps for the model it shows: the entity, clips and baked height.</summary>
    public struct FigureModel
    {
        public Entity Figure;
        public BlobAssetReference<FigureClips> Clips;
        public float Height;
    }

    /// <summary>A figure model a squad figure can show instead of its own.</summary>
    public struct FigureVariant
    {
        /// <summary>Why the variant shows; the base model shows the rest of the time.</summary>
        public VariantReason Reason;
        public FigureModel Model;
    }
}
