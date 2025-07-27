using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Network-aware camera controller that follows the local player
    /// Handles mouse look and camera positioning
    /// </summary>
    public class PlayerCameraController : NetworkBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Transform m_CameraMount;
        //[SerializeField] private float m_MouseSensitivity = 2f; // Not used directly here, but can be used in PlayerController for mouse input
        [SerializeField] private float m_LookXLimit = 80f;
        [SerializeField] private Vector3 m_CameraOffset = new Vector3(0, 1.6f, 0);

        [Header("References")]
        [SerializeField] private Camera m_Camera;

        private void Awake()
        {
            // Find or create camera if not assigned
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
                if (m_Camera == null)
                {
                    GameObject cameraObj = new ("PlayerCamera");
                    m_Camera = cameraObj.AddComponent<Camera>();
                    cameraObj.AddComponent<AudioListener>();
                }
            }

            // Create camera mount if not assigned
            if (m_CameraMount == null)
            {
                GameObject mountObj = new ("CameraMount");
                mountObj.transform.SetParent(transform);
                mountObj.transform.localPosition = m_CameraOffset;
                m_CameraMount = mountObj.transform;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // Disable camera for non-local players
                if (m_Camera != null)
                {
                    m_Camera.gameObject.SetActive(false);
                }
                enabled = false;
                return;
            }

            // Setup camera for local player
            SetupLocalPlayerCamera();

            // Lock cursor
            SetCursorLocked(true);
        }

        private void SetupLocalPlayerCamera()
        {
            // Parent camera to mount point
            m_Camera.transform.SetParent(m_CameraMount);
            m_Camera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Ensure camera is active
            m_Camera.gameObject.SetActive(true);

            // Configure camera settings
            m_Camera.fieldOfView = 90f;
            m_Camera.nearClipPlane = 0.1f;
            m_Camera.farClipPlane = 1000f;
        }

        void LateUpdate()
        {
            if (!IsOwner) return;

            // Get rotation from PlayerController instead
            var playerController = GetComponent<PlayerController>();
            //float rotationY = playerController.GetLookRotationY();

            // Apply only vertical rotation here
            //rotationY = Mathf.Clamp(rotationY, -m_LookXLimit, m_LookXLimit);
            //float deltaRotationY = rotationY * Time.deltaTime;
            //m_CameraMount.localRotation = Quaternion.Euler(deltaRotationY, 0, 0);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                // Unlock cursor when despawning
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            base.OnNetworkDespawn();
        }

        // Utility method to toggle cursor lock (for menus, etc.)
        public void SetCursorLocked(bool locked)
        {
            if (!IsOwner) return;

            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Get camera forward for shooting, etc.
        public Vector3 GetCameraForward()
        {
            return m_Camera != null ? m_Camera.transform.forward : transform.forward;
        }

        public Vector3 GetCameraPosition()
        {
            return m_Camera != null ? m_Camera.transform.position : transform.position;
        }
    }
}