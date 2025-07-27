using Unity.Netcode;
using UnityEngine;
using MultiplayerShooter.Gameplay.Input;
using MultiplayerShooter.Gameplay.Movement;
using MultiplayerShooter.Gameplay.Networking;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Main player controller that orchestrates all player components
    /// Now much simpler and focused on coordination
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerRotation))]
    [RequireComponent(typeof(NetworkInputSync))]
    public class PlayerController : NetworkBehaviour
    {
        // Components
        private PlayerInputHandler m_InputHandler;
        private PlayerMovement m_Movement;
        private PlayerRotation m_Rotation;
        private NetworkInputSync m_NetworkInput;

        // Network variables
        private NetworkVariable<Vector3> m_NetworkPosition = new NetworkVariable<Vector3>();
        private NetworkVariable<Quaternion> m_NetworkRotation = new NetworkVariable<Quaternion>();

        private void Awake()
        {
            // Get all components
            m_InputHandler = GetComponent<PlayerInputHandler>();
            m_Movement = GetComponent<PlayerMovement>();
            m_Rotation = GetComponent<PlayerRotation>();
            m_NetworkInput = GetComponent<NetworkInputSync>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SetupInputHandling();
            }

            if (IsServer)
            {
                SetupServerHandling();
            }

            // Setup network variable callbacks
            m_NetworkPosition.OnValueChanged += OnPositionChanged;
            m_NetworkRotation.OnValueChanged += OnRotationChanged;
        }

        private void SetupInputHandling()
        {
            // Only enable input for owner
            m_InputHandler.enabled = true;

            // Subscribe to input changes
            m_InputHandler.OnMoveInputChanged += HandleMoveInput;
            m_InputHandler.OnLookInputChanged += HandleLookInput;
            m_InputHandler.OnJumpInputChanged += HandleJumpInput;
        }

        private void SetupServerHandling()
        {
            // Subscribe to network input
            m_NetworkInput.OnInputReceived += ProcessNetworkInput;
        }

        private void HandleMoveInput(Vector2 input)
        {
            Vector3 moveDir = new Vector3(input.x, 0, input.y);
            m_NetworkInput.SendInput(moveDir, m_InputHandler.LookInput.x, m_InputHandler.JumpInput);
        }

        private void HandleLookInput(Vector2 input)
        {
            // Local rotation for responsiveness
            m_Rotation.RotateHorizontal(input.x);
            m_Rotation.RotateVertical(input.y);
        }

        private void HandleJumpInput(bool jump)
        {
            Vector3 moveDir = new Vector3(m_InputHandler.MoveInput.x, 0, m_InputHandler.MoveInput.y);
            m_NetworkInput.SendInput(moveDir, m_InputHandler.LookInput.x, jump);
        }

        private void ProcessNetworkInput(NetworkInputSync.InputPayload input)
        {
            if (!IsServer) return;

            // Apply movement
            Vector3 worldMoveDir = transform.TransformDirection(input.moveDirection);
            m_Movement.Move(worldMoveDir);

            if (input.jump)
            {
                m_Movement.Jump();
            }

            // Update network variables
            m_NetworkPosition.Value = transform.position;
            m_NetworkRotation.Value = transform.rotation;
        }

        private void OnPositionChanged(Vector3 oldPos, Vector3 newPos)
        {
            if (!IsOwner && !IsServer)
            {
                // Interpolate position for other clients
                transform.position = newPos;
            }
        }

        private void OnRotationChanged(Quaternion oldRot, Quaternion newRot)
        {
            if (!IsOwner && !IsServer)
            {
                // Apply rotation for other clients
                transform.rotation = newRot;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                m_InputHandler.OnMoveInputChanged -= HandleMoveInput;
                m_InputHandler.OnLookInputChanged -= HandleLookInput;
                m_InputHandler.OnJumpInputChanged -= HandleJumpInput;
            }

            if (IsServer)
            {
                m_NetworkInput.OnInputReceived -= ProcessNetworkInput;
            }

            m_NetworkPosition.OnValueChanged -= OnPositionChanged;
            m_NetworkRotation.OnValueChanged -= OnRotationChanged;

            base.OnNetworkDespawn();
        }
    }
}