using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MultiplayerShooter.Gameplay.Input
{
    /// <summary>
    /// Handles all player input and provides clean interface for other components
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        // Input state
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool JumpInput { get; private set; }
        public bool FireInput { get; private set; }
        public bool ReloadInput { get; private set; }

        // Events for input changes
        public event Action<Vector2> OnMoveInputChanged;
        public event Action<Vector2> OnLookInputChanged;
        public event Action<bool> OnJumpInputChanged;
        public event Action<bool> OnFireInputChanged;
        public event Action<bool> OnReloadInputChanged;

        private PlayerInputActions m_InputActions;

        private void Awake()
        {
            m_InputActions = new PlayerInputActions();
        }

        private void OnEnable()
        {
            m_InputActions.Enable();
            SubscribeToInputEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromInputEvents();
            m_InputActions.Disable();
        }

        private void SubscribeToInputEvents()
        {
            m_InputActions.Player.Move.performed += OnMovePerformed;
            m_InputActions.Player.Move.canceled += OnMoveCanceled;
            m_InputActions.Player.Look.performed += OnLookPerformed;
            m_InputActions.Player.Look.canceled += OnLookCanceled;
            m_InputActions.Player.Jump.performed += OnJumpPerformed;
            m_InputActions.Player.Jump.canceled += OnJumpCanceled;
            m_InputActions.Player.Fire.performed += OnFirePerformed;
            m_InputActions.Player.Fire.canceled += OnFireCanceled;
            m_InputActions.Player.Reload.performed += OnReloadPerformed;
            m_InputActions.Player.Reload.canceled += OnReloadCanceled;
        }

        private void UnsubscribeFromInputEvents()
        {
            m_InputActions.Player.Move.performed -= OnMovePerformed;
            m_InputActions.Player.Move.canceled -= OnMoveCanceled;
            m_InputActions.Player.Look.performed -= OnLookPerformed;
            m_InputActions.Player.Look.canceled -= OnLookCanceled;
            m_InputActions.Player.Jump.performed -= OnJumpPerformed;
            m_InputActions.Player.Jump.canceled -= OnJumpCanceled;
            m_InputActions.Player.Fire.performed -= OnFirePerformed;
            m_InputActions.Player.Fire.canceled -= OnFireCanceled;
            m_InputActions.Player.Reload.performed -= OnReloadPerformed;
            m_InputActions.Player.Reload.canceled -= OnReloadCanceled;
        }

        // Input callbacks
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
            OnMoveInputChanged?.Invoke(MoveInput);
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            MoveInput = Vector2.zero;
            OnMoveInputChanged?.Invoke(MoveInput);
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
            OnLookInputChanged?.Invoke(LookInput);
        }

        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            LookInput = Vector2.zero;
            OnLookInputChanged?.Invoke(LookInput);
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            JumpInput = true;
            OnJumpInputChanged?.Invoke(JumpInput);
        }

        private void OnJumpCanceled(InputAction.CallbackContext context)
        {
            JumpInput = false;
            OnJumpInputChanged?.Invoke(JumpInput);
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            FireInput = true;
            OnFireInputChanged?.Invoke(FireInput);
        }

        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            FireInput = false;
            OnFireInputChanged?.Invoke(FireInput);
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            ReloadInput = true;
            OnReloadInputChanged?.Invoke(ReloadInput);
        }

        private void OnReloadCanceled(InputAction.CallbackContext context)
        {
            ReloadInput = false;
            OnReloadInputChanged?.Invoke(ReloadInput);
        }

        private void OnDestroy()
        {
            m_InputActions?.Dispose();
        }
    }
}