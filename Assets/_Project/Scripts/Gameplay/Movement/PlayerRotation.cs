using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay.Movement
{
    /// <summary>
    /// Handles player and camera rotation separately
    /// </summary>
    public class PlayerRotation : NetworkBehaviour
    {
        [Header("Rotation Settings")]
        [SerializeField] private float m_RotationSpeed = 10f;
        [SerializeField] private float m_LookXLimit = 80f;

        [Header("Camera")]
        [SerializeField] private Transform m_CameraMount;

        // Current rotation values
        private float m_CameraRotationX = 0f;

        // Public properties
        public float HorizontalRotation { get; private set; }
        public float VerticalRotation => m_CameraRotationX;

        public void RotateHorizontal(float amount)
        {
            if (!IsOwner) return;

            HorizontalRotation = amount * m_RotationSpeed;
            transform.Rotate(HorizontalRotation * Time.deltaTime * Vector3.up);
        }

        public void RotateVertical(float amount)
        {
            if (!IsOwner || m_CameraMount == null) return;

            m_CameraRotationX -= amount * m_RotationSpeed * Time.deltaTime;
            m_CameraRotationX = Mathf.Clamp(m_CameraRotationX, -m_LookXLimit, m_LookXLimit);
            m_CameraMount.localRotation = Quaternion.Euler(m_CameraRotationX, 0, 0);
        }

        public void SetCameraMount(Transform mount)
        {
            m_CameraMount = mount;
        }
    }
}