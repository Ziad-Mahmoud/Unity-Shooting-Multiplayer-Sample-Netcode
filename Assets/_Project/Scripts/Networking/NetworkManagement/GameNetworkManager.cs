using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MultiplayerShooter.Infrastructure
{
    /// <summary>
    /// Main network manager setup for arena shooter
    /// Handles connection approval, player spawning, and basic network configuration
    /// </summary>
    public class GameNetworkManager : MonoBehaviour
    {
        [Header("Network Configuration")]
        [SerializeField] private GameObject m_PlayerPrefab;
        [SerializeField] private Transform[] m_SpawnPoints;
        [SerializeField] private int m_MaxPlayers = 8;
        [SerializeField] private float m_ConnectionTimeout = 10f;

        private void Start()
        {
            SetupNetworkManager();
        }

        private void SetupNetworkManager()
        {
            var networkManager = NetworkManager.Singleton;

            // Configure network settings
            networkManager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            networkManager.NetworkConfig.ProtocolVersion = 1; // ushort in Unity 6.1
            networkManager.NetworkConfig.TickRate = 60;
            networkManager.NetworkConfig.ClientConnectionBufferTimeout = (int)m_ConnectionTimeout;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.ConnectionApproval = true;

            // Setup transport for WebGL compatibility
            var transport = networkManager.GetComponent<UnityTransport>();
#if UNITY_WEBGL && !UNITY_EDITOR
            transport.UseWebSockets = true;
            transport.Protocol = UnityTransport.ProtocolType.RelayUnityTransport;
#endif

            // Setup network callbacks
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.ConnectionApprovalCallback = ApprovalCheck;
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Validate connection and check player count
            bool canConnect = NetworkManager.Singleton.ConnectedClients.Count < m_MaxPlayers;

            response.Approved = canConnect;
            response.CreatePlayerObject = canConnect;
            response.PlayerPrefabHash = null; // Use default player prefab
            response.Position = GetSpawnPosition();
            response.Rotation = Quaternion.identity;

            if (!canConnect)
            {
                response.Reason = "Server is full";
            }
        }

        private Vector3 GetSpawnPosition()
        {
            if (m_SpawnPoints.Length == 0) return Vector3.zero;

            int spawnIndex = NetworkManager.Singleton.ConnectedClients.Count % m_SpawnPoints.Length;
            return m_SpawnPoints[spawnIndex].position;
        }

        private void OnServerStarted()
        {
            Debug.Log("Server started successfully");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"Client {clientId} connected");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"Client {clientId} disconnected");
        }
    }
}