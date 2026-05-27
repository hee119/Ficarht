using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class CardObject : MonoBehaviour
{
    [Header("카드 데이터")]
    public CardData data;

    [Header("프리팹 연결")]
    public SpriteRenderer cardFrontRenderer;
    public GameObject cardBackObject;

    [Header("호버 설정")]
    public float hoverHeight = 0.1f;

    public float moveSpeed = 15f;

    [Header("배치 축소 비율")]
    [Range(0.1f, 1f)]
    public float placedScaleMultiplier = 0.7f;

    private Camera cam;

    private Rigidbody rb;

    private bool isDragging;
    private bool isHovered;
    private bool isPlaced;

    private Vector3 originPosition;
    private Vector3 targetPosition;
    private Vector3 dragOffset;

    private Quaternion fanRotation;
    private Quaternion targetRotation;

    private Vector3 originalScale;

    private Quaternion originalRotation;

    private static CardObject currentHoveredCard;

    private void Awake()
    {
        cam = Camera.main;

        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.freezeRotation = true;

        originalScale = transform.localScale;

        originalRotation = transform.rotation;
    }

    private void Update()
    {
        if (isPlaced)
            return;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * moveSpeed
        );

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * moveSpeed
        );

        HandleHover();

        if (!isDragging)
            return;

        DragUpdate();

        if (
            Mouse.current.leftButton
                .wasReleasedThisFrame
        )
        {
            StopDrag();
        }
    }

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void Setup(CardData newData)
    {
        data = newData;

        if (cardFrontRenderer != null)
        {
            cardFrontRenderer.color =
                GetColorByType(data.cardType);
        }

        ShowFront();
    }

    public void InitPosition()
    {
        originPosition = transform.position;

        targetPosition = transform.position;

        fanRotation = transform.rotation;

        targetRotation = transform.rotation;
    }

    public void SetVisible(bool visible)
    {
        foreach (
            var rend
            in GetComponentsInChildren<Renderer>()
        )
        {
            rend.enabled = visible;
        }
    }

    // -------------------------------------------------------
    // 앞면 / 뒷면
    // -------------------------------------------------------
    public void ShowFront()
    {
        if (cardFrontRenderer != null)
        {
            cardFrontRenderer
                .gameObject
                .SetActive(true);
        }

        if (cardBackObject != null)
        {
            cardBackObject.SetActive(false);
        }
    }

    public void ShowBack()
    {
        if (cardFrontRenderer != null)
        {
            cardFrontRenderer
                .gameObject
                .SetActive(false);
        }

        if (cardBackObject != null)
        {
            cardBackObject.SetActive(true);
        }
    }

    // -------------------------------------------------------
    // 슬롯 배치
    // -------------------------------------------------------
    public void PlaceToSlot(
        Vector3 slotPosition,
        Quaternion slotRotation
    )
    {
        isPlaced = true;

        isDragging = false;

        isHovered = false;

        StopAllCoroutines();

        StartCoroutine(
            MoveToSlot(
                slotPosition,
                slotRotation
            )
        );
    }

    private System.Collections.IEnumerator MoveToSlot(
        Vector3 targetPos,
        Quaternion targetRot
    )
    {
        float elapsed = 0f;

        float duration = 0.2f;

        Vector3 startPos =
            transform.position;

        Quaternion startRot =
            transform.rotation;

        Vector3 startScale =
            transform.localScale;

        Vector3 targetScale =
            originalScale
            * placedScaleMultiplier;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                elapsed / duration
            );

            transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                t
            );

            transform.rotation = Quaternion.Lerp(
                startRot,
                targetRot,
                t
            );

            transform.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                t
            );

            yield return null;
        }

        transform.position = targetPos;

        transform.rotation = targetRot;

        transform.localScale = targetScale;
    }

    // -------------------------------------------------------
    // 호버
    // -------------------------------------------------------
    private void HandleHover()
    {
        if (isDragging || isPlaced)
            return;

        Ray ray =
            cam.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        if (
            Physics.Raycast(
                ray,
                out RaycastHit hit
            )
        )
        {
            CardObject card =
                hit.collider.GetComponent<CardObject>();

            if (card == this)
            {
                if (!isHovered)
                {
                    HoverEnter();
                }

                if (
                    Mouse.current
                        .leftButton
                        .wasPressedThisFrame
                )
                {
                    StartDrag();
                }
            }
            else
            {
                if (isHovered)
                {
                    HoverExit();
                }
            }
        }
        else
        {
            if (isHovered)
            {
                HoverExit();
            }
        }
    }

    private void HoverEnter()
    {
        if (
            currentHoveredCard != null &&
            currentHoveredCard != this
        )
        {
            currentHoveredCard.HoverExit();
        }

        currentHoveredCard = this;

        isHovered = true;

        targetPosition = new Vector3(
            originPosition.x,
            originPosition.y + hoverHeight,
            originPosition.z
        );
    }

    private void HoverExit()
    {
        if (isDragging)
            return;

        isHovered = false;

        targetPosition = originPosition;

        targetRotation = fanRotation;

        if (currentHoveredCard == this)
        {
            currentHoveredCard = null;
        }
    }

    // -------------------------------------------------------
    // 드래그 시작
    // -------------------------------------------------------
    public void StartDrag()
    {
        if (
            CardSystemManager.Instance == null ||
            !CardSystemManager.Instance.IsTurnActive
        )
        {
            return;
        }

        isDragging = true;

        isHovered = false;

        targetRotation = Quaternion.Euler(
            fanRotation.eulerAngles.x,
            fanRotation.eulerAngles.y,
            0f
        );

        Ray ray =
            cam.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        Plane plane = new Plane(
            Vector3.up,
            new Vector3(
                0f,
                originPosition.y,
                0f
            )
        );

        if (
            plane.Raycast(
                ray,
                out float distance
            )
        )
        {
            Vector3 hitPoint =
                ray.GetPoint(distance);

            dragOffset =
                transform.position - hitPoint;
        }

        SlotGuideManager.Instance
            ?.ShowGuidesForType(data.cardType);
    }

    // -------------------------------------------------------
    // 드래그 업데이트
    // -------------------------------------------------------
    private void DragUpdate()
    {
        Ray ray =
            cam.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        Plane plane = new Plane(
            Vector3.up,
            new Vector3(
                0f,
                originPosition.y,
                0f
            )
        );

        if (
            plane.Raycast(
                ray,
                out float distance
            )
        )
        {
            Vector3 target =
                ray.GetPoint(distance);

            target += dragOffset;

            target.y =
                originPosition.y
                + hoverHeight;

            targetPosition = target;
        }
    }

    // -------------------------------------------------------
    // 드래그 종료
    // -------------------------------------------------------
    private void StopDrag()
    {
        isDragging = false;

        SlotGuideManager.Instance
            ?.HideAllGuides();

        CardSlot targetSlot =
            FindAvailableSlot();

        if (targetSlot != null)
        {
            bool placed =
                targetSlot.TryPlaceCard(this);

            if (!placed)
            {
                ReturnToOrigin();
            }

            return;
        }

        ReturnToOrigin();
    }

    // -------------------------------------------------------
    // 자동 슬롯 찾기
    // -------------------------------------------------------
    private CardSlot FindAvailableSlot()
    {
        if (CardSystemManager.Instance == null)
            return null;

        List<CardSlot> targetSlots = null;

        switch (data.cardType)
        {
            case CardType.Character:
                targetSlots =
                    CardSystemManager.Instance.characterSlots;
                break;

            case CardType.Buff:
                targetSlots =
                    CardSystemManager.Instance.buffSlots;
                break;

            case CardType.Skill:
                targetSlots =
                    CardSystemManager.Instance.skillSlots;
                break;
        }

        if (targetSlots == null)
            return null;

        foreach (var slot in targetSlots)
        {
            if (
                slot != null &&
                slot.currentCard == null
            )
            {
                return slot;
            }
        }

        return null;
    }

    // -------------------------------------------------------
    // 원위치
    // -------------------------------------------------------
    private void ReturnToOrigin()
    {
        targetPosition = originPosition;

        targetRotation = fanRotation;

        transform.localScale =
            originalScale;
    }

    // -------------------------------------------------------
    // 원래 회전 반환
    // -------------------------------------------------------
    public Quaternion GetOriginalRotation()
    {
        return originalRotation;
    }

    // -------------------------------------------------------
    // 유틸
    // -------------------------------------------------------
    private Color GetColorByType(
        CardType type
    )
    {
        switch (type)
        {
            case CardType.Character:
                return Color.cyan;

            case CardType.Buff:
                return Color.green;

            case CardType.Skill:
                return Color.magenta;

            case CardType.Trap:
                return Color.red;

            default:
                return Color.white;
        }
    }
}