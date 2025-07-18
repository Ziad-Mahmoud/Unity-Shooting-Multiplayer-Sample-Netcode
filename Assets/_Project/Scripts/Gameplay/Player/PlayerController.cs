using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Server-authoritative player controller with client prediction
    /// Implements responsive movement while maintaining server authority
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float m_MoveSpeed = 5f;
        [SerializeField] private float m_RotationSpeed = 10f;
        [SerializeField] private float m_JumpForce = 7f;

        [Header("Network Settings")]
        [SerializeField] private float m_ReconciliationThreshold = 0.1f;

        // Network variables for server authority
        private NetworkVariable<Vector3> m_NetworkPosition = new NetworkVariable<Vector3>();
        private NetworkVariable<Quaternion> m_NetworkRotation = new NetworkVariable<Quaternion>();
        private NetworkVariable<bool> m_IsGrounded = new NetworkVariable<bool>();

        // Client prediction variables
        private Vector3 m_PredictedPosition;
        private Quaternion m_PredictedRotation;
        private Queue<InputCommand> m_InputBuffer = new Queue<InputCommand>();
        private uint m_CurrentInputSequence = 0;

        // Components
        private Rigidbody m_Rigidbody;
        private Collider m_Collider;

        [System.Serializable]
        private struct InputCommand : INetworkSerializable
        {
            public uint sequence;
            public Vector3 moveDirection;
            public float rotationY;
            public bool jump;
            public float timestamp;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref sequence);
                serializer.SerializeValue(ref moveDirection);
                serializer.SerializeValue(ref rotationY);
                serializer.SerializeValue(ref jump);
                serializer.SerializeValue(ref timestamp);
            }
        }

        public override void OnNetworkSpawn()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Collider = GetComponent<Collider>();

            if (IsServer)
            {
                m_NetworkPosition.Value = transform.position;
                m_NetworkRotation.Value = transform.rotation;
            }

            // Setup network variable callbacks
            m_NetworkPosition.OnValueChanged += OnServerPositionChanged;
            m_NetworkRotation.OnValueChanged += OnServerRotationChanged;

            // Initialize predicted values
            m_PredictedPosition = transform.position;
            m_PredictedRotation = transform.rotation;
        }

        void Update()
        {
            if (IsOwner)
            {
                HandleClientInput();
            }
        }

        void FixedUpdate()
        {
            if (IsServer)
            {
                HandleServerMovement();
            }
        }

        private void HandleServerMovement()
        {
            throw new NotImplementedException();
        }

        private void HandleClientInput()
        {
            // Gather input
            var input = new InputCommand
            {
                sequence = m_CurrentInputSequence++,
                moveDirection = GetInputDirection(),
                rotationY = GetInputRotation(),
                jump = Input.GetKey(KeyCode.Space),
                timestamp = Time.time
            };

            // Store input for reconciliation
            m_InputBuffer.Enqueue(input);

            // Apply movement immediately for prediction
            ApplyMovement(input);

            // Send input to server
            SendInputToServerRpc(input);
        }

        private Vector3 GetInputDirection()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 direction = new Vector3(horizontal, 0, vertical);
            return direction.normalized;
        }

        private float GetInputRotation()
        {
            return Input.GetAxis("Mouse X");
        }

        [ServerRpc]
        private void SendInputToServerRpc(InputCommand input)
        {
            // Validate input on server
            if (ValidateInput(input))
            {
                ApplyMovement(input);

                // Update network variables
                m_NetworkPosition.Value = transform.position;
                m_NetworkRotation.Value = transform.rotation;
                m_IsGrounded.Value = CheckGrounded();
            }
        }

        private bool ValidateInput(InputCommand input)
        {
            // Server-side input validation
            // Check for reasonable movement values, rate limiting, etc.
            float maxMoveSpeed = m_MoveSpeed * 1.1f; // Allow some tolerance

            if (input.moveDirection.magnitude > maxMoveSpeed)
            {
                Debug.LogWarning($"Invalid move input from client: {input.moveDirection.magnitude}");
                return false;
            }

            return true;
        }

        private void ApplyMovement(InputCommand input)
        {
            // Calculate movement
            Vector3 moveDirection = transform.TransformDirection(input.moveDirection);
            Vector3 velocity = moveDirection * m_MoveSpeed;

            // Apply movement
            if (IsServer)
            {
                m_Rigidbody.linearVelocity = new Vector3(velocity.x, m_Rigidbody.linearVelocity.y, velocity.z);
            }
            else
            {
                // Client prediction
                m_PredictedPosition += velocity * Time.fixedDeltaTime;
                transform.position = m_PredictedPosition;
            }

            // Apply rotation
            if (Mathf.Abs(input.rotationY) > 0.01f)
            {
                float rotationAmount = input.rotationY * m_RotationSpeed;
                Quaternion deltaRotation = Quaternion.Euler(0, rotationAmount, 0);

                if (IsServer)
                {
                    transform.rotation *= deltaRotation;
                }
                else
                {
                    m_PredictedRotation *= deltaRotation;
                    transform.rotation = m_PredictedRotation;
                }
            }

            // Apply jump
            if (input.jump && CheckGrounded())
            {
                if (IsServer)
                {
                    m_Rigidbody.linearVelocity = new Vector3(m_Rigidbody.linearVelocity.x, m_JumpForce, m_Rigidbody.linearVelocity.z);
                }
            }
        }

        private bool CheckGrounded()
        {
            // Simple ground check
            float rayDistance = m_Collider.bounds.extents.y + 0.1f;
            return Physics.Raycast(transform.position, Vector3.down, rayDistance);
        }

        private void OnServerPositionChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (!IsOwner) return;

            // Reconciliation - check if client prediction differs significantly from server
            float distance = Vector3.Distance(m_PredictedPosition, newValue);

            if (distance > m_ReconciliationThreshold)
            {
                // Snap to server position and replay inputs
                m_PredictedPosition = newValue;
                transform.position = newValue;

                // Here you would typically replay stored inputs
                // For simplicity, we'll just accept the server position
                Debug.Log($"Client position reconciled. Distance: {distance}");
            }
        }

        private void OnServerRotationChanged(Quaternion previousValue, Quaternion newValue)
        {
            if (!IsOwner)
            {
                // Apply server rotation to non-owners
                transform.rotation = newValue;
            }
        }
    }
}