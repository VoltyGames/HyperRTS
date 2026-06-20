using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Core.Cameras
{
    [AddComponentMenu(HyperRTSMenu.Cameras + "Camera Controller")]
    [Icon(HyperRTSIcons.Cameras)]
    [HelpURL(HyperRTSDocs.Roadmap)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Pan speed (WASD) in units per second.")]
        public float moveSpeed = 10f;

        [Tooltip("Rotation speed (Alt + mouse) in degrees per second.")]
        public float rotationSpeed = 100f;

        [Header("Zoom")]
        [Tooltip("Zoom speed (mouse scroll).")]
        public float zoomSpeed = 500f;

        [Tooltip("Closest zoom (minimum camera height).")]
        public float minZoom = 10f;

        [Tooltip("Farthest zoom (maximum camera height).")]
        public float maxZoom = 50f;

        [Header("Default View")]
        [Tooltip("Position the camera resets to (Home key).")]
        public Vector3 defaultPosition = new(0, 35, -50);

        [Tooltip("Rotation the camera resets to (Home key).")]
        public Quaternion defaultRotation = Quaternion.Euler(30, 0, 0);

        // Map Input System device values back to the legacy axis ranges to keep the original feel.
        private const float ScrollNormalization = 0.1f;   // scroll: ~+/-1 per notch -> legacy ~+/-0.1
        private const float MouseDeltaSensitivity = 0.1f; // "Mouse X" default sensitivity

        private void Start()
        {
            ResetViewToDefault();
        }

        [ContextMenu("Capture Current Transform As Default")]
        private void CaptureCurrentTransformAsDefault()
        {
            defaultPosition = transform.position;
            defaultRotation = transform.rotation;
        }

        [ContextMenu("Reset View To Default")]
        private void ResetViewToDefault()
        {
            transform.SetPositionAndRotation(defaultPosition, defaultRotation);
        }

        private void Update()
        {
            HandleMovement();
            HandleRotation();
            HandleZoom();
            HandleReset();
        }

        private void HandleMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var position = transform.position;
            var movement = Vector3.zero;

            // Handle camera movement (restrict Y-axis movement)
            if (keyboard.wKey.isPressed)
            {
                movement += new Vector3(transform.forward.x, 0, transform.forward.z);
            }
            if (keyboard.sKey.isPressed)
            {
                movement -= new Vector3(transform.forward.x, 0, transform.forward.z);
            }
            if (keyboard.aKey.isPressed)
            {
                movement -= new Vector3(transform.right.x, 0, transform.right.z);
            }
            if (keyboard.dKey.isPressed)
            {
                movement += new Vector3(transform.right.x, 0, transform.right.z);
            }

            transform.position += movement * (moveSpeed * Time.deltaTime);

            // Maintain the original Y position (prevent falling to the ground)
            transform.position = new Vector3(transform.position.x, position.y, transform.position.z);
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var scroll = mouse.scroll.ReadValue().y * ScrollNormalization;

            if (scroll != 0)
            {
                var zoom = transform.position;
                zoom.y -= scroll * zoomSpeed * Time.deltaTime;
                zoom.y = Mathf.Clamp(zoom.y, minZoom, maxZoom);
                transform.position = zoom;
            }
        }

        private void HandleRotation()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            if (keyboard.leftAltKey.isPressed)
            {
                var mouseX = mouse.delta.ReadValue().x * MouseDeltaSensitivity;
                var rotationX = mouseX * rotationSpeed * Time.deltaTime;
                transform.Rotate(0, rotationX, 0, Space.World);
            }
        }

        private void HandleReset()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Reset camera position and rotation
            if (keyboard.homeKey.isPressed)
            {
                ResetViewToDefault();
            }
        }
    }
}
