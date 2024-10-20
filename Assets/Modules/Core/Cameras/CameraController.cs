using UnityEngine;

namespace HyperRTS.Core.Cameras
{
    public class CameraController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float zoomSpeed = 500f;
        public float rotationSpeed = 100f;
        public float minZoom = 10f;
        public float maxZoom = 50f;
        public Vector3 defaultPosition = new(0, 35, -50);
        public Quaternion defaultRotation = Quaternion.Euler(30, 0, 0);

        private void Start()
        {
            // Reset camera to default specified position
            transform.position = defaultPosition;
            transform.rotation = defaultRotation;
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
            var position = transform.position;
            var movement = Vector3.zero;
            
            // Handle camera movement (restrict Y-axis movement)
            if (Input.GetKey(KeyCode.W))
            {
                movement += new Vector3(transform.forward.x, 0, transform.forward.z);
            }
            if (Input.GetKey(KeyCode.S))
            {
                movement -= new Vector3(transform.forward.x, 0, transform.forward.z);
            }
            if (Input.GetKey(KeyCode.A))
            {
                movement -= new Vector3(transform.right.x, 0, transform.right.z);
            }
            if (Input.GetKey(KeyCode.D))
            {
                movement += new Vector3(transform.right.x, 0, transform.right.z);
            }

            transform.position += movement * (moveSpeed * Time.deltaTime);
            
            // Maintain the original Y position (prevent falling to the ground)
            transform.position = new Vector3(transform.position.x, position.y, transform.position.z);
        }

        private void HandleZoom()
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            
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
            if (Input.GetKey(KeyCode.LeftAlt))
            {
                var rotationX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                transform.Rotate(0, rotationX, 0, Space.World);
            }
        }

        private void HandleReset()
        {
            // Reset camera position and rotation
            if (Input.GetKey(KeyCode.Home))
            {
                transform.position = defaultPosition;
                transform.rotation = defaultRotation;
            }
        }
    }
}
