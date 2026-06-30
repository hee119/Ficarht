using UnityEngine;
using Mirror;

public class SkyMapManager : MonoBehaviour
{
    [Tooltip("이 Y값 이하로 떨어지면 처리")]
    public float deathY = 0f;

    [Tooltip("낙사 시 입히는 데미지")]
    public float fallDamage = 50f;

    private CharaStat stat;
    private Rigidbody rb;
    private NetworkIdentity networkIdentity;
    private bool isFalling = false;
    private Vector3 spawnPosition;

    private void Awake()
    {
        stat            = GetComponent<CharaStat>();
        rb              = GetComponent<Rigidbody>();
        networkIdentity = GetComponent<NetworkIdentity>();

        if (stat == null) Debug.LogError($"{name} : CharaStat이 NULL입니다.");
        if (rb   == null) Debug.LogError($"{name} : Rigidbody가 NULL입니다.");
    }

    private void Start()
    {
        spawnPosition = transform.position;
        Debug.Log($"{name} 스폰 위치 저장 : {spawnPosition}");
    }

    private void Update()
    {
        if (stat == null || isFalling) return;

        // 자신의 클라이언트만 체크
        if (networkIdentity != null && !networkIdentity.isOwned) return;

        if (transform.position.y <= deathY)
            StartCoroutine(FallProcess());
    }

    private System.Collections.IEnumerator FallProcess()
    {
        isFalling = true;

        // 1. 데미지
        stat.isShield = false;
        stat.Hit(fallDamage);

        // 2. 속도 초기화
        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        // 3. 스폰 위치로 이동
        // NetworkTransform이 위치 동기화를 자동으로 처리
        transform.position = spawnPosition;

        yield return null;

        isFalling = false;
    }
}