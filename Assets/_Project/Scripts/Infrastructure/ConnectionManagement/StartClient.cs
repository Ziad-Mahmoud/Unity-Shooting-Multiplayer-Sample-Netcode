using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class StartClient : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => NetworkManager.Singleton.StartClient());
    }
}
