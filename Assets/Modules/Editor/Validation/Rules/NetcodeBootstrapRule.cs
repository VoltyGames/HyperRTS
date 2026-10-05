using System.Linq;
using Unity.NetCode;
using Unity.Scenes;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A scene hosting a SubScene keeps Netcode from replacing the default world.</summary>
    public sealed class NetcodeBootstrapRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            foreach (var scene in scenes.Scenes.Where(scene => !scene.isSubScene))
            {
                var roots = scene.GetRootGameObjects();
                var subSceneRoot = roots.FirstOrDefault(root => root.GetComponentInChildren<SubScene>(true) != null);
                if (subSceneRoot != null &&
                    !roots.Any(root => root.GetComponentInChildren<OverrideAutomaticNetcodeBootstrap>(true) != null))
                {
                    issues.Warn(subSceneRoot,
                        "No OverrideAutomaticNetcodeBootstrap: Netcode will replace the world. Add the RTS World rig.");
                }
            }
        }
    }
}
