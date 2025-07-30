using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private float m_MouseSensitivity = 2f;
        [SerializeField] private float m_LookXLimit = 80f;
        [SerializeField] private Vector3 m_CameraOffset = new (0, 1.6f, 0);

        [Header("References")]
        [SerializeField] private Camera m_Camera;

        private float m_RotationX = 0;
        private PlayerInputActions m_InputActions;
        private Vector2 m_LookInput;
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
                GameObject mountObj = new GameObject("CameraMount");
                mountObj.transform.SetParent(transform);
                mountObj.transform.localPosition = m_CameraOffset;
                m_CameraMount = mountObj.transform;
            }

            m_InputActions = new PlayerInputActions();
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        private void OnEnable()
        {
            if (!IsOwner) return;

            m_InputActions.Enable();
            m_InputActions.Player.Look.performed += OnLookPerformed;
            m_InputActions.Player.Look.canceled += OnLookCanceled;
        }
        private void OnDisable()
        {
            if (!IsOwner) return;

            m_InputActions.Player.Look.performed -= OnLookPerformed;
            m_InputActions.Player.Look.canceled -= OnLookCanceled;
            m_InputActions.Disable();
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            m_LookInput = context.ReadValue<Vector2>();
        }
        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            m_LookInput = Vector2.zero;
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
            HandleMouseLook();
        }

        private void HandleMouseLook()
        {
            // Apply mouse sensitivity and delta time for frame-independent movement
            float mouseX = m_LookInput.x * m_MouseSensitivity;
            float mouseY = m_LookInput.y * m_MouseSensitivity;

            // Rotate player body horizontally
            transform.Rotate(Vector3.up * mouseX);

            // Rotate camera vertically
            m_RotationX -= mouseY;
            m_RotationX = Mathf.Clamp(m_RotationX, -m_LookXLimit, m_LookXLimit);
            m_CameraMount.localRotation = Quaternion.Euler(m_RotationX, 0, 0);
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