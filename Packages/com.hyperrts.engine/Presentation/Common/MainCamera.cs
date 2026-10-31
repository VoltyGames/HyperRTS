using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Finds the main camera, looking again only once the cached one is gone.</summary>
    public static class MainCamera
    {
        public static Camera Find(Camera cached) => cached != null ? cached : Camera.main;
    }
}
