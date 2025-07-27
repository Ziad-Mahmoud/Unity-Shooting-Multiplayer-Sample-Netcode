using System;
using Unity.Netcode;
using UnityEngine;

namespace MultiplayerShooter.Gameplay.Networking
{
    /// <summary>
    /// Handles network synchronization of player inputs
    /// </summary>
    public class NetworkInputSync : NetworkBehaviour
    {
        [Serializable]
        public struct InputPayload : INetworkSerializable
        {
            public Vector3 moveDirection;
            public float rotationY;
            public bool jump;
            public float timestamp;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref moveDirection);
                serializer.SerializeValue(ref rotationY);
                serializer.SerializeValue(ref jump);
                serializer.SerializeValue(ref timestamp);
            }
        }

        // Events
        public event Action<InputPayload> OnInputReceived;

        // State tracking
        private bool m_HasPendingInput;
        private InputPayload m_PendingInput;

        public void SendInput(Vector3 moveDirection, float rotationY, bool jump)
        {
            if (!IsOwner) return;

            var input = new InputPayload
            {
                moveDirection = moveDirection,
                rotationY = rotationY,
                jump = jump,
                timestamp = Time.time
            };

            SendInputToServerRpc(input);
        }

        [ServerRpc]
        private void SendInputToServerRpc(InputPayload input)
        {
            // Validate input
            if (ValidateInput(input))
            {
                OnInputReceived?.Invoke(input);
            }
        }

        private bool ValidateInput(InputPayload input)
        {
            // Basic validation
            if (input.moveDirection.magnitude > 1.1f)
            {
                Debug.LogWarning($"Invalid move input: {input.moveDirection.magnitude}");
                return false;
            }

            if (Mathf.Abs(input.rotationY) > 100f)
            {
                Debug.LogWarning($"Invalid rotation input: {input.rotationY}");
                return false;
            }

            return true;
        }
    }
}