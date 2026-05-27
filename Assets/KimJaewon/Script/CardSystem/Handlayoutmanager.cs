using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HandLayoutManager : MonoBehaviour
{
    public static HandLayoutManager Instance { get; private set; }

    [Header("플레이어")]
    public Transform playerTransform;

    [Header("플레이어 기준 오프셋")]
    public Vector3 positionOffset = new Vector3(0f, 1.3f, -1.5f);

    [Header("부채꼴 설정")]
    public float cardSpacing = 0.5f;
    public float fanAngle = 30f;

    [Range(0f, 1f)]
    public float overlapAmount = 0.3f;

    [Header("덱 애니메이션")]
    public Transform deckTransform;

    public float flyDuration = 0.4f;
    public float flyDelay = 0.1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // -------------------------------------------------------
    // 중심 위치 계산
    // -------------------------------------------------------
    private Vector3 GetCenterPosition()
    {
        if (playerTransform != null)
        {
            return new Vector3(
                playerTransform.position.x + positionOffset.x,
                playerTransform.position.y + positionOffset.y,
                playerTransform.position.z + positionOffset.z
            );
        }

        return positionOffset;
    }

    // -------------------------------------------------------
    // 드로우 애니메이션
    // -------------------------------------------------------
    public void ArrangeHand(List<CardObject> hand)
    {
        if (hand == null || hand.Count == 0)
            return;

        Vector3 center = GetCenterPosition();

        List<Vector3> targetPositions =
            CalcPositions(hand.Count, center);

        List<Quaternion> targetRotations =
            CalcRotations(hand);

        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null)
                continue;

            StartCoroutine(
                FlyCard(
                    hand[i],
                    targetPositions[i],
                    targetRotations[i],
                    i * flyDelay
                )
            );
        }
    }

    // -------------------------------------------------------
    // 재정렬
    // -------------------------------------------------------
    public void ReArrange(List<CardObject> hand)
    {
        if (hand == null || hand.Count == 0)
            return;

        Vector3 center = GetCenterPosition();

        List<Vector3> targetPositions =
            CalcPositions(hand.Count, center);

        List<Quaternion> targetRotations =
            CalcRotations(hand);

        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null)
                continue;

            StartCoroutine(
                MoveCard(
                    hand[i],
                    targetPositions[i],
                    targetRotations[i]
                )
            );
        }
    }

    // -------------------------------------------------------
    // 카드 날아오기
    // -------------------------------------------------------
    private IEnumerator FlyCard(
        CardObject card,
        Vector3 targetPos,
        Quaternion targetRot,
        float delay
    )
    {
        yield return new WaitForSeconds(delay);

        Vector3 startPos =
            deckTransform != null
                ? deckTransform.position
                : targetPos;

        card.transform.position = startPos;

        card.SetVisible(true);

        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                elapsed / flyDuration
            );

            card.transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                t
            );

            card.transform.rotation = Quaternion.Lerp(
                card.transform.rotation,
                targetRot,
                t
            );

            yield return null;
        }

        card.transform.position = targetPos;
        card.transform.rotation = targetRot;

        card.InitPosition();
    }

    // -------------------------------------------------------
    // 카드 재배치 이동
    // -------------------------------------------------------
    private IEnumerator MoveCard(
        CardObject card,
        Vector3 targetPos,
        Quaternion targetRot
    )
    {
        Vector3 startPos = card.transform.position;

        Quaternion startRot =
            card.transform.rotation;

        float elapsed = 0f;

        float duration = flyDuration * 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                elapsed / duration
            );

            card.transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                t
            );

            card.transform.rotation = Quaternion.Lerp(
                startRot,
                targetRot,
                t
            );

            yield return null;
        }

        card.transform.position = targetPos;
        card.transform.rotation = targetRot;

        card.InitPosition();
    }

    // -------------------------------------------------------
    // 위치 계산
    // -------------------------------------------------------
    private List<Vector3> CalcPositions(
        int count,
        Vector3 center
    )
    {
        List<Vector3> positions =
            new List<Vector3>();

        if (count == 1)
        {
            positions.Add(center);
            return positions;
        }

        float spacing =
            cardSpacing * (1f - overlapAmount);

        float totalWidth =
            spacing * (count - 1);

        float startX =
            center.x - totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            float normalizedPos =
                (float)i / (count - 1);

            float zOffset =
                Mathf.Sin(normalizedPos * Mathf.PI)
                * 0.05f;

            positions.Add(
                new Vector3(
                    startX + spacing * i,
                    center.y,
                    center.z + zOffset
                )
            );
        }

        return positions;
    }

    // -------------------------------------------------------
    // 회전 계산
    // -------------------------------------------------------
    private List<Quaternion> CalcRotations(
        List<CardObject> hand
    )
    {
        List<Quaternion> rotations =
            new List<Quaternion>();

        int count = hand.Count;

        if (count == 1)
        {
            rotations.Add(
                hand[0] != null
                    ? hand[0].GetOriginalRotation()
                    : Quaternion.identity
            );

            return rotations;
        }

        float angleStep =
            fanAngle / (count - 1);

        float startAngle =
            -fanAngle / 2f;

        for (int i = 0; i < count; i++)
        {
            float zAngle =
                startAngle + angleStep * i;

            Quaternion baseRot =
                hand[i] != null
                    ? hand[i].GetOriginalRotation()
                    : Quaternion.identity;

            rotations.Add(
                baseRot *
                Quaternion.Euler(
                    0f,
                    0f,
                    -zAngle
                )
            );
        }

        return rotations;
    }
}