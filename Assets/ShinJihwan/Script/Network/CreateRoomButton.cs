using UnityEngine;

/// <summary>
/// "방 만들기" 3D 오브젝트에 붙이는 버튼.
/// Collider가 반드시 있어야 OnMouseDown이 동작함.
/// </summary>
public class CreateRoomButton : MonoBehaviour
{
    private void OnMouseDown()
    {
        LobbyManager3D.Instance?.CreateRoom();
    }
}
