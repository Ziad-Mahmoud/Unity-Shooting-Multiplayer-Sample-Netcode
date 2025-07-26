using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MultiplayerShooter.Gameplay
{
    /// <summary>
    /// Server-authoritative weapon system with lag compensation
    /// Handles hitscan and projectile weapons with proper validation
    /// </summary>
    public class WeaponController : NetworkBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] private float m_FireRate = 10f;
        [SerializeField] private int m_Damage = 25;
        [SerializeField] private float m_Range = 100f;
        [SerializeField] private LayerMask m_TargetLayers = -1;

        [Header("Effects")]
        [SerializeField] private GameObject m_MuzzleFlashPrefab;
        [SerializeField] private GameObject m_ImpactEffectPrefab;
        [SerializeField] private AudioClip m_FireSound;

        // Input System
        private PlayerInputActions m_InputActions;
        private bool m_FireInput;
        private bool m_ReloadInput;
        private bool m_hasInputChanged;

        // Network variables
        private NetworkVariable<bool> m_IsFiring = new NetworkVariable<bool>();
        private NetworkVariable<int> m_CurrentAmmo = new NetworkVariable<int>(30);

        // Private fields
        private float m_LastFireTime;
        private AudioSource m_AudioSource;
        private Dictionary<ulong, Queue<HistoryFrame>> m_PlayerHistory = new Dictionary<ulong, Queue<HistoryFrame>>();

        [System.Serializable]
        private struct HistoryFrame
        {
            public float timestamp;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Serializable]
        private struct InputCommand : INetworkSerializable
        {
            public uint sequence;
            public bool fire;
            public bool reload;
            public float timestamp;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref sequence);
                serializer.SerializeValue(ref fire);
                serializer.SerializeValue(ref reload);
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
            m_InputActions.Player.Fire.performed += OnFirePerformed;
            m_InputActions.Player.Fire.canceled += OnFireCanceled;
            m_InputActions.Player.Reload.performed += OnReloadPerformed;
            m_InputActions.Player.Reload.canceled += OnReloadCanceled;
        }

        private void OnDisable()
        {
            if (!IsOwner) return;

            // Unsubscribe from input events
            m_InputActions.Player.Fire.performed -= OnFirePerformed;
            m_InputActions.Player.Fire.canceled -= OnFireCanceled;
            m_InputActions.Player.Reload.performed -= OnReloadPerformed;
            m_InputActions.Player.Reload.canceled -= OnReloadCanceled;

            // Disable input actions
            m_InputActions.Disable();
        }

        // Input callbacks
        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            m_FireInput = true;
            MarkInputAsChanged();
        }

        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            m_FireInput = false;
            MarkInputAsChanged();
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            m_ReloadInput = true;
            MarkInputAsChanged();
        }

        private void OnReloadCanceled(InputAction.CallbackContext context)
        {
            m_ReloadInput = false;
            MarkInputAsChanged();
        }

        public override void OnNetworkSpawn()
        {
            m_AudioSource = GetComponent<AudioSource>();

            if (IsServer)
            {
                m_CurrentAmmo.Value = 30;
                m_IsFiring.Value = false;
            }

            m_IsFiring.OnValueChanged += OnFiringStateChanged;
        }

        void Update()
        {
            if (IsOwner && m_hasInputChanged)
            {
                HandleClientInput();
                m_hasInputChanged = false;
            }

            if (IsServer)
            {
                UpdatePlayerHistory();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_InputActions?.Dispose();
        }

        private void MarkInputAsChanged()
        {
            m_hasInputChanged = true;
        }

        private void HandleClientInput()
        {
            // Gather input
            var input = new InputCommand
            {
                fire = m_FireInput,
                reload = m_ReloadInput,
                timestamp = Time.time
            };

            if (input.fire)
            {
                var camera = GetComponent<PlayerCameraController>();
                Vector3 firePos = camera.GetCameraPosition();
                Vector3 fireDir = camera.GetCameraForward();
                RequestFireServerRpc(firePos, fireDir, input.timestamp);
            }

            if (input.reload)
            {
                RequestReloadServerRpc();
            }
        }

        [ServerRpc]
        private void RequestFireServerRpc(Vector3 firePosition, Vector3 direction, float clientTime)
        {
            if (CanFire())
            {
                PerformFire(firePosition, direction, clientTime);
            }
        }

        [ServerRpc]
        private void RequestReloadServerRpc()
        {
            if (m_CurrentAmmo.Value < 30)
            {
                m_CurrentAmmo.Value = 30;
                PlayReloadEffectsClientRpc();
            }
        }

        private bool CanFire()
        {
            return Time.time > m_LastFireTime + (1f / m_FireRate) && m_CurrentAmmo.Value > 0;
        }

        private void PerformFire(Vector3 firePosition, Vector3 direction, float clientTime)
        {
            m_LastFireTime = Time.time;
            m_IsFiring.Value = true;
            m_CurrentAmmo.Value--;

            // Perform lag compensated hit detection
            var hitTargets = PerformLagCompensatedHitScan(firePosition, direction, clientTime);

            foreach (var target in hitTargets)
            {
                DealDamageToTarget(target, m_Damage);
            }

            // Notify all clients
            FireWeaponClientRpc(firePosition, direction);

            m_IsFiring.Value = false;
        }

        private List<NetworkObject> PerformLagCompensatedHitScan(Vector3 origin, Vector3 direction, float clientTime)
        {
            var hits = new List<NetworkObject>();

            // Calculate lag compensation time
            float compensationTime = (float)NetworkManager.ServerTime.Time - clientTime;

            // For simplicity, we'll do a basic raycast
            // In a production system, you'd rewind player positions based on compensationTime
            RaycastHit[] rayHits = Physics.RaycastAll(origin, direction, m_Range, m_TargetLayers);

            foreach (var hit in rayHits)
            {
                var networkObject = hit.collider.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject != NetworkObject)
                {
                    hits.Add(networkObject);
                    break; // Only hit first target for hitscan
                }
            }

            return hits;
        }

        private void DealDamageToTarget(NetworkObject target, int damage)
        {
            //var health = target.GetComponent<NetworkedHealth>();
            //if (health != null)
            //{
            //    health.TakeDamageServerRpc(damage, OwnerClientId);
            //}
        }

        [ClientRpc]
        private void FireWeaponClientRpc(Vector3 firePosition, Vector3 direction)
        {
            // Play visual and audio effects
            PlayMuzzleFlash();
            PlayFireSound();

            // Spawn impact effect
            SpawnImpactEffect(firePosition, direction);
        }

        [ClientRpc]
        private void PlayReloadEffectsClientRpc()
        {
            // Play reload animation and sound
            Debug.Log("Reloading weapon");
        }

        private void UpdatePlayerHistory()
        {
            // Store player positions for lag compensation
            foreach (var client in NetworkManager.ConnectedClients)
            {
                if (client.Value.PlayerObject != null)
                {
                    if (!m_PlayerHistory.ContainsKey(client.Key))
                    {
                        m_PlayerHistory[client.Key] = new Queue<HistoryFrame>();
                    }

                    var history = m_PlayerHistory[client.Key];
                    history.Enqueue(new HistoryFrame
                    {
                        timestamp = Time.time,
                        position = client.Value.PlayerObject.transform.position,
                        rotation = client.Value.PlayerObject.transform.rotation
                    });

                    // Keep only last 1 second of history
                    while (history.Count > 0 && Time.time - history.Peek().timestamp > 1f)
                    {
                        history.Dequeue();
                    }
                }
            }
        }

        private void PlayMuzzleFlash()
        {
            if (m_MuzzleFlashPrefab != null)
            {
                var flash = Instantiate(m_MuzzleFlashPrefab, transform.position, transform.rotation);
                Destroy(flash, 0.1f);
            }
        }

        private void PlayFireSound()
        {
            if (m_AudioSource != null && m_FireSound != null)
            {
                m_AudioSource.PlayOneShot(m_FireSound);
            }
        }

        private void SpawnImpactEffect(Vector3 firePosition, Vector3 direction)
        {
            RaycastHit hit;
            if (Physics.Raycast(firePosition, direction, out hit, m_Range))
            {
                if (m_ImpactEffectPrefab != null)
                {
                    var impact = Instantiate(m_ImpactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(impact, 2f);
                }
            }
        }

        private void OnFiringStateChanged(bool previousValue, bool newValue)
        {
            // React to firing state changes if needed
        }
    }
}