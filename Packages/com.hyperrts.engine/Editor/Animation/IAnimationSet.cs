using System.Collections.Generic;

namespace HyperRTS.Editor.Animation
{
    /// <summary>An animation set that bakes its textures and tables, which the bake commands find by type.</summary>
    public interface IAnimationSet
    {
        /// <summary>Bakes the set and returns the paths of the assets written.</summary>
        List<string> Bake();
    }
}
