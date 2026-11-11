using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>A figure model or the deployed model that a squad's update shows or hides. Squads run in parallel and write only themselves, so one job applies these afterwards.</summary>
    public struct FigureHide
    {
        public Entity Root;
        public Entity Branch;
        public bool Hide;
    }
}
