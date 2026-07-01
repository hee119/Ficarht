using UnityEngine;
using Mirror;

/// <summary>
/// Main Camera에 붙여두면 스폰된 로컬 플레이어 캐릭터를 자동으로 추적합니다.
///
/// ■ 싱글 플레이: PlayerController 가 있는 오브젝트를 찾아 추적
/// ■ Mirror 멀티: isOwned == true 인 PlayerController 를 추적
/// ■ 마우스 좌우로 카메라가 플레이어 주위를 회전
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상 (비우면 자동 탐색)")]
    public Transform target;

    [Header("카메라 오프셋 (플레이어 기준)")]
    public Vector3 offset = new Vector3(0f, 5f, -4f);

    [Header("회전 각도 (X축 고정 피치)")]
    public float pitchAngle = 45f;

    [Header("부드러운 이동 속도 (높을수록 빠름)")]
    public float smoothSpeed = 8f;

    [Header("마우스 감도")]
    public float mouseSensitivity = 5f;

    [Header("벽 충돌 방지")]
    [Tooltip("카메라와 플레이어 사이 장애물 감지 레이어")]
    public LayerMask wallLayers = ~0;           // 기본: 모든 레이어
    [Tooltip("벽에 닿았을 때 카메라를 플레이어 쪽으로 당기는 여유 거리")]
    public float wallOffset = 0.3f;

    [Header("자동 탐색 재시도 간격 (초)")]
    public float searchInterval = 0.5f;

    private float _searchTimer = 0f;
    private float _yaw = 0f;   // 카메라 수평 회전값

    // ─────────────────────────────────────────────
    void Start()
    {
        // 현재 카메라 yaw를 초기값으로 설정
        _yaw = transform.eulerAngles.y;
        TryFindTarget();
    }

    void LateUpdate()
    {
        // 마우스 수평 이동으로 카메라 Yaw 업데이트 (CameraFollow가 직접 처리)
        _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;

        // 타겟이 없거나 파괴된 경우 재탐색
        if (target == null)
        {
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                _searchTimer = searchInterval;
                TryFindTarget();
            }
            return;
        }

        // pitch(수직고정) + yaw(마우스 수평) 로 카메라 위치 계산
        Vector3 desiredPos = target.position + Quaternion.Euler(pitchAngle, _yaw, 0f) * offset;

        // ── 벽 관통 방지: 플레이어 → desiredPos 방향으로 SphereCast ──
        Vector3 lookAt   = target.position + Vector3.up * 1.5f;
        Vector3 dir      = desiredPos - lookAt;
        float   maxDist  = dir.magnitude;

        Vector3 finalPos = desiredPos;
        if (Physics.SphereCast(lookAt, 0.2f, dir.normalized, out RaycastHit hit,
                               maxDist, wallLayers, QueryTriggerInteraction.Ignore))
        {
            // 장애물 앞으로 카메라를 끌어당김
            finalPos = hit.point + hit.normal * wallOffset;
        }

        // 스무딩 이동
        transform.position = Vector3.Lerp(transform.position, finalPos, smoothSpeed * Time.deltaTime);

        // 플레이어를 바라봄
        transform.LookAt(lookAt);
    }

    // 외부에서 Yaw를 조정해야 할 때 사용 (현재는 LateUpdate에서 직접 처리)
    public void AddYawDelta(float delta)
    {
        _yaw += delta * mouseSensitivity;
    }

    // ─────────────────────────────────────────────
    // 로컬 플레이어 탐색
    // ─────────────────────────────────────────────
    private void TryFindTarget()
    {
        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var pc in controllers)
        {
            // 비활성화된 컴포넌트 무시 (전투씬에서 숨겨진 로비 PlayerNetwork 오브젝트 제외)
            if (!pc.enabled) continue;

            // Mirror 멀티플레이: 내 소유 캐릭터만
            if (NetworkClient.active)
            {
                if (pc.isOwned)
                {
                    target = pc.transform;
                    return;
                }
            }
            else
            {
                // 싱글: 첫 번째 PlayerController
                target = pc.transform;
                return;
            }
        }
    }

    // ─────────────────────────────────────────────
    // 외부에서 직접 타겟 지정할 때 사용
    // ─────────────────────────────────────────────
    public void SetTarget(Transform t)
    {
        target = t;
    }
}
