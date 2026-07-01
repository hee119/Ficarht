using UnityEngine;
using Mirror;

public class PlayerRespawner : NetworkBehaviour
{
    [Command]
    public void CmdTeleportTo(Vector3 destination)
    {
        Debug.Log($"[PlayerRespawner] 서버에서 텔레포트 실행 → {destination}");

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = destination;
        }

        transform.position = destination;

        if (rb != null)
            rb.isKinematic = false;

        // 오너 클라이언트 본인은 매 프레임 자기 위치를 서버로 보내는 쪽(client-authoritative)
        // 이라서 서버가 여기서 transform.position만 바꿔도 본인 화면에는 반영되지 않는다.
        // 직접 텔레포트를 지시해야 낙하 위치로 되돌아가지 않는다.
        // (오너 위치가 고쳐지면 기존 20Hz 동기화 루프가 자동으로 다른 클라이언트에도 전파한다.)
        TargetTeleport(destination);
    }

    [TargetRpc]
    private void TargetTeleport(Vector3 destination)
    {
        var rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = destination;
        }

        transform.position = destination;

        if (rb != null) rb.isKinematic = false;
    }
}
