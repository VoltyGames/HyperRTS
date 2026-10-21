using Unity.Entities;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The <see cref="DebrisSet"/> a prop throws. Enabled, it throws once the first frame it is drawn (ruins); a
    /// building with breakable parts keeps it disabled and throws from each part that breaks.
    /// </summary>
    public struct Debris : IComponentData, IEnableableComponent
    {
        public UnityObjectRef<DebrisSet> Set;

        /// <summary>Metres from the centre the pieces start within.</summary>
        public float Radius;
    }
}
