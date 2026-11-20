using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// The engine's code-driven shaders, which URP's graphics settings find by name, fill in and keep in player builds.
    /// </summary>
    [Serializable]
    [HideInInspector]
    [Category("Resources/HyperRTS Shaders")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class EngineShaders : IRenderPipelineResources
    {
        [SerializeField, HideInInspector] private int version;

        [SerializeField, ResourcePath("Hidden/HyperRTS/FogOfWar", SearchType.ShaderName)]
        private Shader fogOfWar;

        [SerializeField, ResourcePath("Hidden/HyperRTS/ContactShadows", SearchType.ShaderName)]
        private Shader contactShadows;

        [SerializeField, ResourcePath("Hidden/HyperRTS/Sharpen", SearchType.ShaderName)]
        private Shader sharpen;

        [SerializeField, ResourcePath("Hidden/HyperRTS/CraterHeight", SearchType.ShaderName)]
        private Shader craterHeight;

        [SerializeField, ResourcePath("Hidden/HyperRTS/CloudCookie", SearchType.ShaderName)]
        private Shader cloudCookie;

        [SerializeField, ResourcePath("Hidden/HyperRTS/TrampleBrush", SearchType.ShaderName)]
        private Shader trampleBrush;

        [SerializeField, ResourcePath("HyperRTS/Foliage", SearchType.ShaderName)]
        private Shader foliage;

        int IRenderPipelineGraphicsSettings.version => version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        public Shader FogOfWar => fogOfWar;

        public Shader ContactShadows => contactShadows;

        public Shader Sharpen => sharpen;

        public Shader CraterHeight => craterHeight;

        public Shader CloudCookie => cloudCookie;

        public Shader TrampleBrush => trampleBrush;

        public Shader Foliage => foliage;

        /// <summary>The project's copy from URP's global settings; throws when the active pipeline isn't URP.</summary>
        public static EngineShaders Get()
        {
            if (!GraphicsSettings.TryGetRenderPipelineSettings<EngineShaders>(out var shaders))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: URP's global settings have no {nameof(EngineShaders)}; the render pipeline must be URP.");
            }

            return shaders;
        }
    }
}
