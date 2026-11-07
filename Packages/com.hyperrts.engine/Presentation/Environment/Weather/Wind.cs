using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>
    /// Publishes the scene's wind for flags and vegetation (Wind.hlsl). The <see cref="WindZone"/> gives strength,
    /// turbulence, gust amount (windPulseMagnitude) and gust tiles per second (windPulseFrequency).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(WindZone))]
    [AddComponentMenu(HyperRTSMenu.Scene + "Environment/Wind")]
    [Icon(HyperRTSIcons.Wind)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed partial class Wind : PresentationBehaviour
    {
        private const int GustMapSize = 128;

        private static readonly int DirectionId = Shader.PropertyToID("_HyperRTSWindDirection");
        private static readonly int StrengthId = Shader.PropertyToID("_HyperRTSWindStrength");
        private static readonly int GustId = Shader.PropertyToID("_HyperRTSWindGust");
        private static readonly int GustOffsetId = Shader.PropertyToID("_HyperRTSWindGustOffset");
        private static readonly int GustMapId = Shader.PropertyToID("_HyperRTSWindGustMap");
        private static readonly int DetailId = Shader.PropertyToID("_HyperRTSWindDetail");

        [Tooltip("Metres of ground one repeat of the gust field covers.")]
        [Min(1f)]
        public float gustSize = 120f;

        [Tooltip("How much trees, grass and crops move in this wind. None keeps them still but for downwash and blasts. Flags still fly.")]
        public WindDetail vegetation = WindDetail.Flutter;

        private WindZone _zone;
        private Texture2D _gustMap;
        private Vector2 _gustOffset;
        private World _world;
        private Entity _state;

        private void OnEnable()
        {
            _zone = GetComponent<WindZone>();
            _gustMap = GustField.CreateMap(GustMapSize);
            Shader.SetGlobalTexture(GustMapId, _gustMap);
        }

        private void Update()
        {
            var forward = transform.forward;
            var downwind = new Vector3(forward.x, 0f, forward.z).normalized;
            var last = _gustOffset;
            _gustOffset = GustField.Scroll(_gustOffset, new Vector2(downwind.x, downwind.z), _zone.windPulseFrequency, Time.deltaTime);

            Publish(new WindState { Downwind = downwind, Strength = _zone.windMain });
            Shader.SetGlobalVector(DirectionId, downwind);
            Shader.SetGlobalFloat(StrengthId, _zone.windMain);
            Shader.SetGlobalVector(GustId, new Vector4(_zone.windPulseMagnitude, _zone.windTurbulence, 1f / gustSize, 0f));
            Shader.SetGlobalVector(GustOffsetId, new Vector4(_gustOffset.x, _gustOffset.y, last.x, last.y));
            Shader.SetGlobalFloat(DetailId, (float)vegetation);
        }

        protected override void OnDisabled()
        {
            Unpublish();
            Shader.SetGlobalFloat(StrengthId, 0f);
            Shader.SetGlobalTexture(GustMapId, Texture2D.blackTexture);
            Shader.SetGlobalFloat(DetailId, (float)WindDetail.None);
            CoreUtils.Destroy(_gustMap);
        }

        // Shader globals outlive Play mode in the editor.
        [OnEnteringPlayMode]
        private static void Reset() => Shader.SetGlobalFloat(StrengthId, 0f);

        // Retried every frame: the world may not exist yet, or may be a new one.
        private void Publish(in WindState wind)
        {
            if (!MatchView.TryGetDefault(out var view))
            {
                return;
            }

            var manager = view.EntityManager;
            if (_world != manager.World || !manager.Exists(_state))
            {
                _world = manager.World;
                _state = manager.CreateEntity(typeof(WindState));
            }

            manager.SetComponentData(_state, wind);
        }

        // Destroyed rather than zeroed, so readers see still air by the singleton being absent.
        private void Unpublish()
        {
            if (_world is { IsCreated: true } && _world.EntityManager.Exists(_state))
            {
                _world.EntityManager.DestroyEntity(_state);
            }

            _world = null;
            _state = Entity.Null;
        }
    }
}
