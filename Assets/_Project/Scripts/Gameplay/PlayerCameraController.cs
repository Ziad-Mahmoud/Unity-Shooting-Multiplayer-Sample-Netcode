using Unity.Netcode;
using UnityEngine;
using MultiplayerShooter.Gameplay.Input;
using MultiplayerShooter.Gameplay.Weapons;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Simplified weapon controller that orchestrates weapon components
    /// </summary>
    [RequireComponent(typeof(WeaponFiringSystem))]
    [RequireComponent(typeof(WeaponEffectsSystem))]
    public class WeaponController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerCameraController m_CameraController;

        // Components
        private PlayerInputHandler m_InputHandler;
        private WeaponFiringSystem m_FiringSystem;
        private WeaponEffectsSystem m_EffectsSystem;

        // State
        private bool m_FireHeld;

        private void Awake()
        {
            m_InputHandler = GetComponentInParent<PlayerInputHandler>();
            m_FiringSystem = GetComponent<WeaponFiringSystem>();
            m_EffectsSystem = GetComponent<WeaponEffectsSystem>();

            if (m_CameraController == null)
                m_CameraController = GetComponentInParent<PlayerCameraController>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            // Subscribe to input events
            if (m_InputHandler != null)
            {
                m_InputHandler.OnFireInputChanged += HandleFireInput;
                m_InputHandler.OnReloadInputChanged += HandleReloadInput;
            }
        }

        private void HandleFireInput(bool fire)
        {
            m_FireHeld = fire;
        }

        private void HandleReloadInput(bool reload)
        {
            if (reload)
            {
                m_FiringSystem.RequestReloadServerRpc();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (m_FireHeld && m_FiringSystem.CanFire)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (m_CameraController == null) return;

            Vector3 origin = m_CameraController.GetCameraPosition();
            Vector3 direction = m_CameraController.GetCameraForward();

            m_FiringSystem.RequestFireServerRpc(origin, direction);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && m_InputHandler != null)
            {
                m_InputHandler.OnFireInputChanged -= HandleFireInput;
                m_InputHandler.OnReloadInputChanged -= HandleReloadInput;
            }

            base.OnNetworkDespawn();
        }
    }
}