using UnityEngine;

/// <summary>
/// "방 참가" 3D 오브젝트에 붙이는 버튼.
/// 클릭하면 키보드 코드 입력 모드 시작.
/// Collider가 반드시 있어야 OnMouseDown이 동작함.
/// </summary>
public class JoinRoomButton : MonoBehaviour
{
    private void OnMouseDown()
    {
        LobbyManager3D.Instance?.StartJoinMode();
    }
}
