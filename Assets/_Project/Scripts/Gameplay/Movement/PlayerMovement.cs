using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay.Movement
{
    /// <summary>
    /// Handles player movement logic separately from input
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float m_MoveSpeed = 5f;
        [SerializeField] private float m_JumpForce = 7f;
        [SerializeField] private float m_GroundCheckDistance = 0.1f;

        // Components
        private Rigidbody m_Rigidbody;
        private Collider m_Collider;

        // State
        private bool m_IsGrounded;

        // Public properties
        public bool IsGrounded => m_IsGrounded;
        public Vector3 Velocity => m_Rigidbody.linearVelocity;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Collider = GetComponent<Collider>();
        }

        public void Move(Vector3 direction)
        {
            if (!IsServer) return;

            Vector3 velocity = direction * m_MoveSpeed;
            m_Rigidbody.linearVelocity = new Vector3(velocity.x, m_Rigidbody.linearVelocity.y, velocity.z);
        }

        public void Jump()
        {
            if (!IsServer || !m_IsGrounded) return;

            m_Rigidbody.linearVelocity = new Vector3(
                m_Rigidbody.linearVelocity.x,
                m_JumpForce,
                m_Rigidbody.linearVelocity.z
            );
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();
        }

        private void UpdateGroundedState()
        {
            float rayDistance = m_Collider.bounds.extents.y + m_GroundCheckDistance;
            m_IsGrounded = Physics.Raycast(transform.position, Vector3.down, rayDistance);
        }

        // Client prediction support
        public void ApplyVelocity(Vector3 velocity)
        {
            if (IsServer) return; // Server uses physics

            transform.position += velocity * Time.fixedDeltaTime;
        }
    }
}