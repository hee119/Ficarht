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
    }
}
