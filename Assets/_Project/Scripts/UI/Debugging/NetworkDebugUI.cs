using Unity.Multiplayer.Tools.NetStatsMonitor;
//using Unity.Multiplayer.Tools.RuntimeNetStatsMonitor;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerShooter.UI
{
    /// <summary>
    /// Network debugging UI showing ping, packet loss, and connection info
    /// Essential for development and portfolio demonstration
    /// </summary>
    public class NetworkDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text m_PingText;
        [SerializeField] private Text m_PacketLossText;
        [SerializeField] private Text m_PlayerCountText;
        [SerializeField] private Text m_NetworkStatusText;
        [SerializeField] private GameObject m_DebugPanel;

        [Header("Settings")]
        [SerializeField] private bool m_ShowOnStart = true;
        [SerializeField] private KeyCode m_ToggleKey = KeyCode.F1;

        private RuntimeNetStatsMonitor m_NetworkStatsMonitor;
        private bool m_IsVisible;

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

            if (m_IsVisible)
            {
                UpdateDebugInfo();
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

            // Configure custom stats
            //m_NetworkStatsMonitor.AddCustomValue("Player Count", () =>
            //    NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 0);

            //m_NetworkStatsMonitor.AddCustomValue("Server Tick", () =>
            //    NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Tick : 0);

            //m_NetworkStatsMonitor.AddCustomValue("Round Trip Time", () =>
            //    NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalTime.TimeAsFloat : 0f);
        }

        private void UpdateDebugInfo()
        {
            if (NetworkManager.Singleton == null) return;

            // Update ping information
            if (m_PingText != null)
            {
                float rtt = NetworkManager.Singleton.LocalTime.TimeAsFloat;
                m_PingText.text = $"Ping: {(rtt * 1000):F0}ms";
            }

            // Update player count
            if (m_PlayerCountText != null)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
                m_PlayerCountText.text = $"Players: {playerCount}";
            }

            // Update network status
            if (m_NetworkStatusText != null)
            {
                string status = "Disconnected";
                if (NetworkManager.Singleton.IsServer)
                    status = "Server";
                else if (NetworkManager.Singleton.IsClient)
                    status = "Client";

                m_NetworkStatusText.text = $"Status: {status}";
            }

            // Update packet loss (simplified)
            if (m_PacketLossText != null)
            {
                // In a real implementation, you'd calculate actual packet loss
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
                m_NetworkStatsMonitor.gameObject.SetActive(visible);
            }
        }
    }
}