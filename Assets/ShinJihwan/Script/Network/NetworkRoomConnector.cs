using UnityEngine;
using Mirror;

public class NetworkRoomConnector : MonoBehaviour
{
    [Header("기본 접속 주소 (로컬 테스트용)")]
    [SerializeField] private string defaultAddress = "localhost";

    /// <summary>
    /// [기능 1] 방 만들기 (Host로 시작)
    /// </summary>
    public void CreateRoom()
    {
        // 현재 네트워크가 실행 중이지 않을 때만 가동
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkManager.singleton.StartHost();
            Debug.Log("🚀 [서버 소식] Host 서버가 가동되었습니다. 방 생성 완료.");
        }
    }

    /// <summary>
    /// [기능 2] 방 참여하기 (Client로 시작)
    /// </summary>
    /// <param name="ipAddress">입력받은 상대방 IP 주소 (빈 값이면 localhost로 접속)</param>
    public void JoinRoom(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress?.Trim()))
        {
            Debug.LogWarning("[NetworkRoomConnector] 방 코드를 입력하세요.");
            return;
        }

        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkManager.singleton.networkAddress = ipAddress.Trim();
            NetworkManager.singleton.StartClient();
            Debug.Log($"🌐 [서버 소식] {NetworkManager.singleton.networkAddress} 주소로 연결을 시도합니다.");
        }
    }

    /// <summary>
    /// [기능 3] 방 나가기 (연결 끊기)
    /// </summary>
    public void LeaveRoom()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            // 내가 방장이면 서버 전체 종료 (방 폭파)
            NetworkManager.singleton.StopHost();
            Debug.Log("❌ [서버 소식] Host가 연결을 종료하여 방이 파괴되었습니다.");
        }
        else if (NetworkClient.isConnected)
        {
            // 내가 손님이였으면 나만 퇴장
            NetworkManager.singleton.StopClient();
            Debug.Log("🏃 [서버 소식] 방에서 퇴장했습니다.");
        }
    }
}