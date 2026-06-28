using UnityEngine;
using Mirror;

public class NetworkLobbyController : MonoBehaviour
{
    [SerializeField] private string defaultAddress = "localhost"; // 로컬 테스트용 기본 IP

    // 1. 방 만들기 (Host로 시작: 서버와 클라이언트를 동시에 실행)
    public void CreateRoom()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            // 현재 작동 중인 네트워크가 없다면 Host 시작
            NetworkManager.singleton.StartHost();
            Debug.Log("방이 생성되었습니다 (Host 시작).");
        }
    }

    // 2. 방 참여하기 (Guest/Client로 시작)
    // ipAddress 파라미터에 상대방 IP를 넣습니다. 빈 값이면 기본 주소(localhost) 사용.
    public void JoinRoom(string ipAddress)
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            // 접속할 주소 설정
            if (string.IsNullOrEmpty(ipAddress))
            {
                NetworkManager.singleton.networkAddress = defaultAddress;
            }
            else
            {
                NetworkManager.singleton.networkAddress = ipAddress;
            }

            // Client 시작
            NetworkManager.singleton.StartClient();
            Debug.Log($"{NetworkManager.singleton.networkAddress} 주소의 방에 참여를 시도합니다.");
        }
    }

    // 3. 연결 끊기 (방 나가기 또는 연결 취소)
    public void LeaveRoom()
    {
        // Host인 경우 서버와 클라이언트 모두 종료
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
            Debug.Log("방을 삭제하고 나갔습니다.");
        }
        // Guest인 경우 클라이언트만 종료
        else if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
            Debug.Log("방에서 퇴장했습니다.");
        }
    }
}