using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>The enabled <see cref="AmbientLife"/>, as a singleton; absent means no ambient life.</summary>
    public struct AmbientLifeSource : IComponentData
    {
        public UnityObjectRef<AmbientLife> Life;
    }
}
