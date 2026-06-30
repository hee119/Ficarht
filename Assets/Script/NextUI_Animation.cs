using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class NextUI_Animation : MonoBehaviour
{
    [Header("Targets")]
    public GameObject[] targetObjects;

    [Header("Fall Settings")]
    public float fallStartY = 500f;
    public float duration = 0.5f;
    public Ease ease = Ease.OutBack;

    [Tooltip("애니메이션 완료 후 버튼 활성화까지 추가 대기 시간 (초)")]
    public float clickDelay = 0.3f;

    private Vector3[] startPositions;

    [Header("NextUI")]
    public GameObject nextUI;

    // 애니메이션 진행 중 여부 — Next_UI() 및 외부에서 체크용
    public bool IsAnimating { get; private set; }

    private void OnEnable()
    {
        PlayFallAnimation();
    }

    private void OnDisable()
    {
        // 패널이 닫힐 때 진행 중인 트윈을 모두 킬하고 위치·콜라이더 복원
        DOTween.Kill(this);
        if (targetObjects != null && startPositions != null)
        {
            for (int i = 0; i < targetObjects.Length && i < startPositions.Length; i++)
            {
                if (targetObjects[i] != null)
                    targetObjects[i].transform.position = startPositions[i];
            }
        }
        IsAnimating = false;
        EnableColliders(true);
    }

    public void PlayFallAnimation()
    {
        if (targetObjects == null || targetObjects.Length == 0)
            return;

        // 이전 트윈이 남아있으면 먼저 킬 (패널 열림→닫힘→열림 시 충돌 방지)
        DOTween.Kill(this);

        startPositions = new Vector3[targetObjects.Length];

        // 애니메이션 중 모든 Collider 비활성화 (M3D 버튼 클릭 차단)
        EnableColliders(false);
        IsAnimating = true;

        Sequence sequence = DOTween.Sequence().SetId(this);

        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] == null)
                continue;

            Transform target = targetObjects[i].transform;

            // 최종 위치를 현재 위치로 기록 (OnDisable 복원용)
            startPositions[i] = target.position;

            Vector3 fallStartPosition = startPositions[i];
            fallStartPosition.y = this.gameObject.transform.position.y + fallStartY;

            target.position = fallStartPosition;

            sequence.Append(
                target.DOMove(startPositions[i], duration)
                    .SetEase(ease)
            );
        }

        // 애니메이션 완료 → clickDelay 후 콜라이더 복구, 클릭 허용
        sequence.OnComplete(() =>
        {
            DOVirtual.DelayedCall(clickDelay, () =>
            {
                IsAnimating = false;
                EnableColliders(true);
            });
        });
    }

    private void EnableColliders(bool enable)
    {
        if (targetObjects == null) return;
        foreach (var obj in targetObjects)
            if (obj != null)
                foreach (var c in obj.GetComponentsInChildren<Collider>(true))
                    c.enabled = enable;
    }

    public void Next_UI()
    {
        // 애니메이션 중 클릭 무시
        if (IsAnimating) return;

        if (nextUI != null)
            nextUI.SetActive(true);

        gameObject.SetActive(false);
    }
}