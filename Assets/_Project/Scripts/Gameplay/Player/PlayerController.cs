using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

        // Input System
        private PlayerInputActions m_InputActions;
        private Vector2 m_MoveInput;
        private Vector2 m_LookInput;
        private bool m_JumpInput;
        private bool m_hasInputChanged;

        // Components
        private Rigidbody m_Rigidbody;
        private Collider m_Collider;

        [Serializable]
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
        private void Awake()
        {
            // Initialize Input System
            m_InputActions = new PlayerInputActions();
        }

        private void OnEnable()
        {
            if (!IsOwner) return;

            // Enable input actions and subscribe to events
            m_InputActions.Enable();

            // Subscribe to input events
            m_InputActions.Player.Move.performed += OnMovePerformed;
            m_InputActions.Player.Move.canceled += OnMoveCanceled;
            m_InputActions.Player.Look.performed += OnLookPerformed;
            m_InputActions.Player.Look.canceled += OnLookCanceled;
            m_InputActions.Player.Jump.performed += OnJumpPerformed;
            m_InputActions.Player.Jump.canceled += OnJumpCanceled;
        }

        private void OnDisable()
        {
            if (!IsOwner) return;

            // Unsubscribe from input events
            m_InputActions.Player.Move.performed -= OnMovePerformed;
            m_InputActions.Player.Move.canceled -= OnMoveCanceled;
            m_InputActions.Player.Look.performed -= OnLookPerformed;
            m_InputActions.Player.Look.canceled -= OnLookCanceled;
            m_InputActions.Player.Jump.performed -= OnJumpPerformed;
            m_InputActions.Player.Jump.canceled -= OnJumpCanceled;

            // Disable input actions
            m_InputActions.Disable();
        }

        // Input callbacks
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            m_MoveInput = context.ReadValue<Vector2>();
            MarkInputAsChanged();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            m_MoveInput = Vector2.zero;
            MarkInputAsChanged();
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            m_LookInput = context.ReadValue<Vector2>();
            MarkInputAsChanged();
        }

        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            m_LookInput = Vector2.zero;
            MarkInputAsChanged();
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            m_JumpInput = true;
            MarkInputAsChanged();
        }

        private void OnJumpCanceled(InputAction.CallbackContext context)
        {
            m_JumpInput = false;
            MarkInputAsChanged();
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
            if (IsOwner && m_hasInputChanged)
            {
                HandleClientInput();
                m_hasInputChanged = false; // Reset input change flag
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
            // Update network variables with current transform state
            m_NetworkPosition.Value = transform.position;
            m_NetworkRotation.Value = transform.rotation;
            m_IsGrounded.Value = CheckGrounded();
        }

        private void HandleClientInput()
        {
            // Convert 2D input to 3D movement direction
            Vector3 moveDirection = new Vector3(m_MoveInput.x, 0, m_MoveInput.y);

            // Gather input
            var input = new InputCommand
            {
                sequence = m_CurrentInputSequence++,
                moveDirection = moveDirection.normalized,
                rotationY = m_LookInput.x,
                jump = m_JumpInput,
                timestamp = Time.time
            };

            // Store input for reconciliation
            m_InputBuffer.Enqueue(input);

            // Limit input buffer size
            while (m_InputBuffer.Count > 60) // Store last 1 second at 60 tick rate
            {
                m_InputBuffer.Dequeue();
            }

            // Apply movement immediately for prediction
            ApplyMovement(input);

            // Send input to server
            SendInputToServerRpc(input);
        }

        private void MarkInputAsChanged()
        {
            m_hasInputChanged = true;
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
            // Check if movement direction is within valid range (normalized vector)
            if (input.moveDirection.magnitude > 1.1f) // Allow 10% tolerance for floating point errors
            {
                Debug.LogWarning($"Invalid move input from client: {input.moveDirection.magnitude}");
                return false;
            }

            // Additional validation: check for unreasonable rotation values
            if (Mathf.Abs(input.rotationY) > 100f) // Arbitrary max rotation speed
            {
                Debug.LogWarning($"Invalid rotation input from client: {input.rotationY}");
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
                    m_Rigidbody.linearVelocity = new Vector3(
                        m_Rigidbody.linearVelocity.x,
                        m_JumpForce,
                        m_Rigidbody.linearVelocity.z
                    );
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

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_InputActions?.Dispose();

            // Unsubscribe from network variable callbacks
            m_NetworkPosition.OnValueChanged -= OnServerPositionChanged;
            m_NetworkRotation.OnValueChanged -= OnServerRotationChanged;
        }
    }
}