using Mirror;
using UnityEngine;

public class AutoStartNetwork : MonoBehaviour
{
    void Start()
    {
        Debug.Log("🔥 Host 자동 시작");
        NetworkManager.singleton.StartHost();
    }
}