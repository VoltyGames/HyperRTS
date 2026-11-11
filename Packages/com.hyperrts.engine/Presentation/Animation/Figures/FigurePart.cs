using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>A renderer that plays a squad figure's clips: which figure it follows and the last clip change it took from it. Baked.</summary>
    public struct FigurePart : IComponentData
    {
        public Entity Root;

        /// <summary>Index of the figure in the squad's <see cref="SquadFigure"/> buffer.</summary>
        public int Index;

        /// <summary>The figure model, base or other, this part belongs to; it plays only while that model is shown.</summary>
        public Entity Model;

        /// <summary>The figure's <see cref="SquadFigure.ClipEdits"/> this part last took.</summary>
        public uint Applied;
    }
}
