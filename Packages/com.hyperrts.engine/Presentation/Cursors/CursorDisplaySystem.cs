using HyperRTS.Client.Interaction;
using Unity.Entities;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>Shows this world's <see cref="CursorIntent"/> through the display a game hands it; none keeps the OS pointer.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CursorIntentSystem))]
    public partial class CursorDisplaySystem : SystemBase
    {
        public CursorDisplay Display { get; set; }

        protected override void OnCreate() => RequireForUpdate<CursorIntent>();

        protected override void OnUpdate() => Display?.Show(SystemAPI.GetSingleton<CursorIntent>().Kind);
    }
}
