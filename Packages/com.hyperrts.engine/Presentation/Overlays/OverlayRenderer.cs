using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Draws selection rings, health bars, the placement ghost and rally points with instanced meshes.</summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Overlay Renderer")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class OverlayRenderer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("HyperRTS/Overlay material; tinted per overlay through _BaseColor.")]
        private Material material;

        [SerializeField]
        [Tooltip("Overlay colours and sizes.")]
        private OverlayStyle style = new();

        private OverlayMaterials _materials;
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

            _materials ??= new OverlayMaterials(material);

            _units.Gather(view, style, camera.transform);
            _commands.Gather(view, style);
            _units.Draw(_materials, style, gameObject.layer);
            _commands.Draw(material, style, gameObject.layer);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            _materials?.Dispose();
        }
    }
}
