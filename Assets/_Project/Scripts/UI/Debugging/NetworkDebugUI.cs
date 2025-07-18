using Unity.Multiplayer.Tools.NetStatsMonitor;
using Unity.Netcode;
using UnityEngine;
using TMPro; // TextMeshPro namespace

namespace MultiplayerShooter.UI
{
    /// <summary>
    /// Network debugging UI showing ping, packet loss, and connection info
    /// Uses TextMeshProUGUI for modern UI rendering
    /// </summary>
    public class NetworkDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI m_PingText;
        [SerializeField] private TextMeshProUGUI m_PacketLossText;
        [SerializeField] private TextMeshProUGUI m_PlayerCountText;
        [SerializeField] private TextMeshProUGUI m_NetworkStatusText;
        [SerializeField] private TextMeshProUGUI m_ServerTickText;
        [SerializeField] private TextMeshProUGUI m_ClientIdText;
        [SerializeField] private GameObject m_DebugPanel;

        [Header("Settings")]
        [SerializeField] private bool m_ShowOnStart = true;
        [SerializeField] private KeyCode m_ToggleKey = KeyCode.F1;
        [SerializeField] private float m_UpdateInterval = 0.1f; // Update UI every 100ms

        private RuntimeNetStatsMonitor m_NetworkStatsMonitor;
        private bool m_IsVisible;
        private float m_LastUpdateTime;

        void Start()
        {
            SetupNetworkStatsMonitor();
            SetDebugVisibility(m_ShowOnStart);
        }

        void Update()
        {
            if (Input.GetKeyDown(m_ToggleKey))
            {
                ToggleDebugUI();
            }

            if (m_IsVisible && Time.time - m_LastUpdateTime > m_UpdateInterval)
            {
                UpdateDebugInfo();
                m_LastUpdateTime = Time.time;
            }
        }

        private void SetupNetworkStatsMonitor()
        {
            // Setup Unity's Runtime Network Stats Monitor
            if (m_NetworkStatsMonitor == null)
            {
                var debugObject = new GameObject("NetworkStatsMonitor");
                m_NetworkStatsMonitor = debugObject.AddComponent<RuntimeNetStatsMonitor>();
                DontDestroyOnLoad(debugObject);
            }

            // Configure position and style
            m_NetworkStatsMonitor.Position = RuntimeNetStatsMonitor.DisplayPosition.TopLeft;
            m_NetworkStatsMonitor.MaxRefreshRate = 10f; // 10 updates per second
            m_NetworkStatsMonitor.Visible = m_ShowOnStart;
        }

        private void UpdateDebugInfo()
        {
            if (NetworkManager.Singleton == null) return;

            // Update ping information
            if (m_PingText != null)
            {
                if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    // Calculate approximate RTT for clients
                    float localTime = NetworkManager.Singleton.LocalTime.TimeAsFloat;
                    float serverTime = NetworkManager.Singleton.ServerTime.TimeAsFloat;
                    float rtt = Mathf.Abs(serverTime - localTime) * 2f; // Approximate RTT
                    m_PingText.text = $"Ping: {(rtt * 1000):F0}ms";
                }
                else if (NetworkManager.Singleton.IsServer)
                {
                    m_PingText.text = "Ping: Host";
                }
                else
                {
                    m_PingText.text = "Ping: N/A";
                }
            }

            // Update player count
            if (m_PlayerCountText != null)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
                int maxPlayers = 8; // You can make this configurable
                m_PlayerCountText.text = $"Players: {playerCount}/{maxPlayers}";
            }

            // Update network status
            if (m_NetworkStatusText != null)
            {
                string status = "Disconnected";
                Color statusColor = Color.red;

                if (NetworkManager.Singleton.IsHost)
                {
                    status = "Host";
                    statusColor = Color.green;
                }
                else if (NetworkManager.Singleton.IsServer)
                {
                    status = "Server";
                    statusColor = Color.cyan;
                }
                else if (NetworkManager.Singleton.IsClient)
                {
                    status = "Client";
                    statusColor = Color.yellow;
                }

                m_NetworkStatusText.text = $"Status: {status}";
                m_NetworkStatusText.color = statusColor;
            }

            // Update server tick
            if (m_ServerTickText != null)
            {
                if (NetworkManager.Singleton.IsConnectedClient)
                {
                    uint serverTick = NetworkManager.Singleton.ServerTime.Tick;
                    m_ServerTickText.text = $"Server Tick: {serverTick}";
                }
                else
                {
                    m_ServerTickText.text = "Server Tick: N/A";
                }
            }

            // Update client ID
            if (m_ClientIdText != null)
            {
                if (NetworkManager.Singleton.IsConnectedClient)
                {
                    ulong clientId = NetworkManager.Singleton.LocalClientId;
                    m_ClientIdText.text = $"Client ID: {clientId}";
                }
                else
                {
                    m_ClientIdText.text = "Client ID: N/A";
                }
            }

            // Update packet loss (simplified)
            if (m_PacketLossText != null)
            {
                // In a real implementation, you'd track sent vs received packets
                // For now, we'll show a placeholder
                m_PacketLossText.text = "Packet Loss: 0%";
            }
        }

        private void ToggleDebugUI()
        {
            SetDebugVisibility(!m_IsVisible);
        }

        private void SetDebugVisibility(bool visible)
        {
            m_IsVisible = visible;

            if (m_DebugPanel != null)
            {
                m_DebugPanel.SetActive(visible);
            }

            if (m_NetworkStatsMonitor != null)
            {
                m_NetworkStatsMonitor.Visible = visible;
            }
        }

        // Optional: Add method to update custom metrics if needed
        public void UpdateCustomMetric(string metricName, float value)
        {
            // This can be used to display custom game-specific metrics
            // You would need additional UI elements to display these
        }
    }
}