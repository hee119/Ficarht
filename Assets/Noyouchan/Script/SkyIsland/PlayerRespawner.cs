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

        // PlayerController는 NetworkTransform 없이 자체 SyncVar(_syncPos/_syncRot)로
        // 위치를 동기화하므로, 서버 쪽 transform만 바꿔서는 오너 클라이언트나 다른
        // 클라이언트에게 반영되지 않는다. SyncVar도 함께 갱신해야 함.
        var pc = GetComponent<PlayerController>();
        if (pc != null)
            pc.ServerForceSyncPosition(destination, transform.rotation);

        // 오너 클라이언트 본인에게는 SyncVar 보간이 아니라 직접 텔레포트를 지시해야
        // CharacterController가 이전 낙하 위치로 되돌리지 않는다.
        TargetTeleport(destination);
    }

    [TargetRpc]
    private void TargetTeleport(Vector3 destination)
    {
        var cc = GetComponent<CharacterController>();
        var rb = GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = destination;

        if (rb != null) rb.isKinematic = false;
        if (cc != null) cc.enabled = true;
    }
}
