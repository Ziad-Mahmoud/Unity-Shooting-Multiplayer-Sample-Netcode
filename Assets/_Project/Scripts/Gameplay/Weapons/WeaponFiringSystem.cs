using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay.Weapons
{
    /// <summary>
    /// Handles weapon firing mechanics
    /// </summary>
    public class WeaponFiringSystem : NetworkBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] private float m_FireRate = 10f;
        [SerializeField] private int m_Damage = 25;
        [SerializeField] private float m_Range = 100f;
        [SerializeField] private LayerMask m_TargetLayers = -1;

        // Network variables
        private readonly NetworkVariable<int> m_CurrentAmmo = new (30);

        // State
        private float m_LastFireTime;

        // Properties
        public int CurrentAmmo => m_CurrentAmmo.Value;
        public bool CanFire => Time.time > m_LastFireTime + (1f / m_FireRate) && m_CurrentAmmo.Value > 0;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_CurrentAmmo.Value = 30;
            }
        }

        [ServerRpc]
        public void RequestFireServerRpc(Vector3 origin, Vector3 direction)
        {
            if (!CanFire) return;

            PerformFire(origin, direction);
        }

        [ServerRpc]
        public void RequestReloadServerRpc()
        {
            m_CurrentAmmo.Value = 30;
            OnReloadClientRpc();
        }

        private void PerformFire(Vector3 origin, Vector3 direction)
        {
            m_LastFireTime = Time.time;
            m_CurrentAmmo.Value--;

            // Perform hit detection
            if (Physics.Raycast(origin, direction, out RaycastHit hit, m_Range, m_TargetLayers))
            {
                var target = hit.collider.GetComponent<NetworkObject>();
                if (target != null && target != NetworkObject)
                {
                    // Deal damage (implement health system separately)
                    Debug.Log($"Hit {target.name} for {m_Damage} damage");
                }
            }

            // Notify clients for effects
            OnWeaponFiredClientRpc(origin, direction, hit.point, hit.normal);
        }

        [ClientRpc]
        private void OnWeaponFiredClientRpc(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal)
        {
            // Let the effects system handle this
            SendMessage("PlayFireEffects", SendMessageOptions.DontRequireReceiver);

            if (hitPoint != Vector3.zero)
            {
                SendMessage("PlayImpactEffects", new object[] { hitPoint, hitNormal }, SendMessageOptions.DontRequireReceiver);
            }
        }

        [ClientRpc]
        private void OnReloadClientRpc()
        {
            SendMessage("PlayReloadEffects", SendMessageOptions.DontRequireReceiver);
        }
    }
}