using UnityEngine;
using Mirror;
using System.Collections;

public class SkyMapManager : MonoBehaviour
{
    [Tooltip("이 Y값 이하로 떨어지면 처리")]
    public float deathY = 0f;

    [Tooltip("낙사 시 입히는 데미지")]
    public float fallDamage = 50f;

    private Transform player;
    private CharaStat stat;
    private Rigidbody rb;
    private PlayerRespawner respawner;
    private Vector3 spawnPosition;
    private bool spawnFound = false;
    private bool ready = false;
    private bool isFalling = false;
    private float searchLogTimer = 0f;

    private void Update()
    {
        if (!ready)
        {
            TryFindPlayer();
            return;
        }

        if (isFalling || player == null) return;

        if (player.position.y <= deathY)
            StartCoroutine(FallProcess());
    }

    private void TryFindPlayer()
    {
        Transform found = null;

        if (NetworkClient.localPlayer != null)
            found = NetworkClient.localPlayer.transform;

        if (found == null)
        {
            foreach (var ni in FindObjectsOfType<NetworkIdentity>())
            {
                if (ni.isLocalPlayer) { found = ni.transform; break; }
            }
        }

        if (found == null)
        {
            var chara = FindObjectOfType<CharaStat>();
            if (chara != null) found = chara.transform;
        }

        if (found == null)
        {
            searchLogTimer += Time.deltaTime;
            if (searchLogTimer >= 3f)
            {
                searchLogTimer = 0f;
                Debug.LogWarning("[SkyMap] 플레이어를 찾지 못했습니다. CharaStat이 씬에 있는지 확인하세요.");
            }
            return;
        }

        player    = found;
        stat      = found.GetComponent<CharaStat>();
        rb        = found.GetComponent<Rigidbody>();
        respawner = found.GetComponent<PlayerRespawner>();

        if (stat == null) Debug.LogError("[SkyMap] CharaStat 없음");

        string mySpawnID = NetworkServer.active ? "spawn_P1" : "spawn_P2";
        foreach (var sp in FindObjectsOfType<SpawnPoint>())
        {
            if (sp.spawnID == mySpawnID)
            {
                spawnPosition = sp.transform.position;
                spawnFound = true;
                break;
            }
        }

        if (!spawnFound)
            Debug.LogError($"[SkyMap] SpawnPoint '{mySpawnID}' 없음");

        ready = true;
    }

    private IEnumerator FallProcess()
    {
        isFalling = true;

        if (stat != null)
        {
            stat.isShield = false;
            stat.Hit(fallDamage);
        }

        if (spawnFound)
        {
            if (respawner != null && NetworkClient.active)
            {
                respawner.CmdTeleportTo(spawnPosition);
            }
            else
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = spawnPosition;
                }
                player.position = spawnPosition;

                yield return new WaitForFixedUpdate();

                if (rb != null) rb.isKinematic = false;
                if (cc != null) cc.enabled = true;
            }
        }

        yield return new WaitForSeconds(0.5f);
        isFalling = false;
    }
}
