using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Network-aware camera controller using Cinemachine v3
    /// Provides smooth camera movement and professional feel
    /// </summary>
    public class PlayerCameraController : NetworkBehaviour
    {
        [Header("Cinemachine Settings")]
        [SerializeField] private CinemachineCamera m_VirtualCamera;
        [SerializeField] private Transform m_CameraTarget; // What the camera looks at
        [SerializeField] private Vector3 m_CameraOffset = new(0, 1.6f, -4f);

        [Header("Look Settings")]
        [SerializeField] private float m_MouseSensitivity = 2f;
        [SerializeField] private float m_LookXLimit = 80f;
        [SerializeField] private bool m_InvertY = false;

        [Header("Camera Shake")]
        [SerializeField] private float m_FireShakeAmplitude = 0.5f;
        [SerializeField] private float m_FireShakeDuration = 0.1f;

        // Components
        private CinemachineBasicMultiChannelPerlin m_CameraShake;
        private Transform m_CameraRig;
        private Camera m_MainCamera;

        // Input
        private PlayerInputActions m_InputActions;
        private Vector2 m_LookInput;

        // Rotation state
        private float m_RotationX = 0f;
        private float m_RotationY = 0f;

        private void Awake()
        {
            m_InputActions = new PlayerInputActions();

            // Find main camera
            m_MainCamera = Camera.main;
            if (m_MainCamera == null)
            {
                Debug.LogError("No main camera found! Creating one...");
                GameObject cameraObj = new("MainCamera");
                m_MainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
                cameraObj.tag = "MainCamera";
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // Disable camera components for non-local players
                if (m_VirtualCamera != null)
                {
                    m_VirtualCamera.gameObject.SetActive(false);
                }
                enabled = false;
                return;
            }

            SetupCinemachine();
            SetCursorLocked(true);
        }

        private void SetupCinemachine()
        {
            // Create camera rig structure
            CreateCameraRig();

            // Setup Cinemachine virtual camera if not assigned
            if (m_VirtualCamera == null)
            {
                GameObject vcamObj = new("PlayerVirtualCamera");
                vcamObj.transform.SetParent(m_CameraRig);
                vcamObj.transform.localPosition = Vector3.zero;

                m_VirtualCamera = vcamObj.AddComponent<CinemachineCamera>();
                m_VirtualCamera.Priority = 10;
            }

            // Configure virtual camera
            ConfigureVirtualCamera();

            // Add noise for camera shake
            var noise = m_VirtualCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 1;
            m_CameraShake = noise;
        }

        private void CreateCameraRig()
        {
            // Create camera rig hierarchy for smooth rotation
            GameObject rigObj = new("CameraRig");
            rigObj.transform.SetParent(transform);
            rigObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_CameraRig = rigObj.transform;

            // Create follow target
            if (m_CameraTarget == null)
            {
                GameObject targetObj = new("CameraTarget");
                targetObj.transform.SetParent(m_CameraRig);
                targetObj.transform.localPosition = m_CameraOffset;
                m_CameraTarget = targetObj.transform;
            }
        }

        private void ConfigureVirtualCamera()
        {
            // Set follow and look at targets
            m_VirtualCamera.Follow = m_CameraTarget;
            m_VirtualCamera.LookAt = null; // We'll handle rotation manually

            // Configure lens
            m_VirtualCamera.Lens.FieldOfView = 90f;
            m_VirtualCamera.Lens.NearClipPlane = 0.1f;
            m_VirtualCamera.Lens.FarClipPlane = 1000f;

            // Setup position composer for smooth following
            if (!m_VirtualCamera.TryGetComponent<CinemachinePositionComposer>(out var positionComposer))
            {
                positionComposer = m_VirtualCamera.gameObject.AddComponent<CinemachinePositionComposer>();
            }

            positionComposer.Damping = new Vector3(0.5f, 0.5f, 0.5f);
            positionComposer.CameraDistance = 0; // We use the target offset instead

            // Add rotation composer for smooth rotation
            if (!m_VirtualCamera.TryGetComponent(out CinemachineRotationComposer _))
            {
                m_VirtualCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            }
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

        void LateUpdate()
        {
            if (!IsOwner) return;
            HandleCameraRotation();
        }

        private void HandleCameraRotation()
        {
            // Get mouse input with sensitivity
            float mouseX = m_LookInput.x * m_MouseSensitivity;
            float mouseY = m_LookInput.y * m_MouseSensitivity * (m_InvertY ? -1 : 1);

            // Rotate player body horizontally
            m_RotationY += mouseX;
            transform.rotation = Quaternion.Euler(0, m_RotationY, 0);

            // Rotate camera rig vertically
            m_RotationX -= mouseY;
            m_RotationX = Mathf.Clamp(m_RotationX, -m_LookXLimit, m_LookXLimit);

            if (m_CameraRig != null)
            {
                m_CameraRig.localRotation = Quaternion.Euler(m_RotationX, 0, 0);
            }
        }

        /// <summary>
        /// Trigger camera shake (e.g., when firing weapon)
        /// </summary>
        public void TriggerCameraShake(float amplitude = -1, float duration = -1)
        {
            if (!IsOwner || m_CameraShake == null) return;

            float shakeAmp = amplitude > 0 ? amplitude : m_FireShakeAmplitude;
            float shakeDur = duration > 0 ? duration : m_FireShakeDuration;

            StartCoroutine(CameraShakeCoroutine(shakeAmp, shakeDur));
        }

        private System.Collections.IEnumerator CameraShakeCoroutine(float amplitude, float duration)
        {
            m_CameraShake.AmplitudeGain = amplitude;
            yield return new WaitForSeconds(duration);
            m_CameraShake.AmplitudeGain = 0;
        }

        /// <summary>
        /// Set camera FOV for zooming
        /// </summary>
        public void SetFieldOfView(float fov, float transitionTime = 0.2f)
        {
            if (!IsOwner || m_VirtualCamera == null) return;

            // Cinemachine will smoothly transition the FOV
            m_VirtualCamera.Lens.FieldOfView = fov;
        }

        /// <summary>
        /// Get camera forward direction for shooting
        /// </summary>
        public Vector3 GetCameraForward()
        {
            return m_MainCamera != null ? m_MainCamera.transform.forward : transform.forward;
        }

        /// <summary>
        /// Get camera position for shooting origin
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            return m_MainCamera != null ? m_MainCamera.transform.position : transform.position;
        }

        /// <summary>
        /// Toggle cursor lock state
        /// </summary>
        public void SetCursorLocked(bool locked)
        {
            if (!IsOwner) return;

            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SetCursorLocked(false);

                // Disable virtual camera
                if (m_VirtualCamera != null)
                {
                    m_VirtualCamera.Priority = 0;
                }
            }

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            m_InputActions?.Dispose();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Visualize camera offset
            Gizmos.color = Color.blue;
            Vector3 cameraPos = transform.position + transform.TransformDirection(m_CameraOffset);
            Gizmos.DrawWireSphere(cameraPos, 0.2f);
            Gizmos.DrawLine(transform.position, cameraPos);
        }
#endif
    }
}