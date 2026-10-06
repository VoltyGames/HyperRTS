using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Draws selection rings, health bars, the placement ghost and rally points with instanced meshes.</summary>
    [AddComponentMenu(HyperRTSMenu.UI + "Overlay Renderer")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class OverlayRenderer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Transparent, instancing-enabled unlit material; tinted per overlay through _BaseColor.")]
        private Material material;

        [SerializeField]
        [Tooltip("Overlay colours and sizes.")]
        private OverlayStyle style = new();

        private Material _front;
        private Mesh[] _meshes;
        private UnitOverlays _units;
        private CommandOverlays _commands;

        private void Awake()
        {
            var ring = OverlayMeshes.Ring(48, 0.82f);
            var billboard = OverlayMeshes.Billboard();
            var box = OverlayMeshes.Box();
            var flat = OverlayMeshes.FlatQuad();
            _meshes = new[] { ring, billboard, box, flat };
            _units = new UnitOverlays(ring, billboard);
            _commands = new CommandOverlays(box, ring, flat);
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (material == null || camera == null || !MatchView.TryGetDefault(out var view))
            {
                return;
            }

            // A later queue keeps bar fills drawn over their backgrounds regardless of depth sorting.
            if (_front == null)
            {
                _front = new Material(material) { renderQueue = material.renderQueue + 1, hideFlags = HideFlags.DontSave };
            }

            _units.Gather(view, style, camera.transform);
            _commands.Gather(view, style);
            _units.Draw(material, _front, style, gameObject.layer);
            _commands.Draw(material, style, gameObject.layer);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            if (_front != null)
            {
                Destroy(_front);
            }
        }
    }
}
