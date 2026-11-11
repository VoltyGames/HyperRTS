using HyperRTS.Presentation.Rendering.Parts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial struct SquadFigureSystem
    {
        /// <summary>Applies the squads' hides on one thread; they change only with a figure's state, and write parts of other entities.</summary>
        [BurstCompile]
        private struct HideJob : IJob
        {
            public NativeQueue<FigureHide> Hides;
            public RenderSwitch Switch;

            public void Execute()
            {
                while (Hides.TryDequeue(out var hide))
                {
                    Switch.SetBranch(hide.Root, hide.Branch, HideReason.Figure, hide.Hide);
                }
            }
        }
    }
}
