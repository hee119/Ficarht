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

    [Range(1f, 1.5f)]
    public float hoverScaleMultiplier = 1.08f;

    public float handHoverScreenLift = 80f;

    [Header("드래그 높이")]
    public float dragHeight = 0.35f;

    public int hoverSortingOrderBoost = 100;

    public float moveSpeed = 15f;

    [Header("배치 축소 비율")]
    [Range(0.1f, 1f)]
    public float placedScaleMultiplier = 0.7f;

    [Header("타입별 배치 크기")]
    [Range(0.1f, 1f)]
    public float characterPlacedScale = 0.68f;

    [Range(0.1f, 1f)]
    public float buffPlacedScale = 0.58f;

    [Range(0.1f, 1f)]
    public float trapPlacedScale = 0.4f;

    [Header("드래그 판정")]
    public float dragThreshold = 50f;

    public float dragHoldTime = 0.08f;

    [Header("드롭 최소 이동 거리")]
    public float minDropDistance = 1.2f;

    private Camera cam;

    private Rigidbody rb;

    private bool isDragging;
    private bool isPlaced;

    private CardSlot placedSlot;

    private bool isMouseDown;

    private Vector2 mouseDownPosition;

    private float mouseDownTimer;

    private Vector3 originPosition;
    private Vector3 targetPosition;
    private Vector3 dragOffset;

    private Vector3 dragStartWorldPos;

    private Quaternion fanRotation;
    private Quaternion targetRotation;

    private Vector3 originalScale;

    private Vector3 targetScale;

    private Quaternion originalRotation;

    private Renderer[] cardRenderers;

    private int[] originalSortingOrders;

    private bool isRenderOrderBoosted;

    private static CardObject currentHoveredCard;

    private static readonly RaycastHit[] hoverHits =
        new RaycastHit[32];

    private static int lastHoverUpdateFrame = -1;

    private static Vector2 pointerPosition;

    private static bool pointerPressedThisFrame;

    private static bool pointerReleasedThisFrame;

    private static bool pointerIsPressed;

    private void Awake()
    {
        cam = Camera.main;

        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.freezeRotation = true;

        originalScale = transform.localScale;

        targetScale = originalScale;

        originalRotation = transform.rotation;

        // targetRotation 미초기화 시 (0,0,0,0) 제로 쿼터니언이 되어
        // Quaternion.Lerp에서 Assertion 에러 발생 → 반드시 초기화
        targetRotation = transform.rotation;
        targetPosition = transform.position;

        CacheRenderers();
    }

    private void Update()
    {
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

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * moveSpeed
        );

        UpdateHoverState();

        if (isPlaced)
        {
            HandlePlacedClick();
            return;
        }

        HandleHoverInput();

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

        targetScale = originalScale;

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
        Quaternion slotRotation,
        CardSlot slot = null
    )
    {
        isPlaced = true;

        placedSlot = slot;

        targetPosition = slotPosition;

        targetRotation = slotRotation;

        targetScale = GetRestScale();

        isDragging = false;

        isMouseDown = false;

        mouseDownTimer = 0f;

        if (currentHoveredCard == this)
        {
            currentHoveredCard = null;
            CardTooltipUI.Instance?.Hide();
        }

        RestoreRenderOrder();

        StopAllCoroutines();

        StartCoroutine(
            MoveToSlot(
                slotPosition,
                slotRotation
            )
        );
    }

    public void ReturnToHand()
    {
        isPlaced = false;

        isDragging = false;

        isMouseDown = false;

        mouseDownTimer = 0f;

        placedSlot = null;

        if (currentHoveredCard == this)
        {
            currentHoveredCard = null;
            CardTooltipUI.Instance?.Hide();
        }

        targetPosition = transform.position;

        targetRotation = transform.rotation;

        targetScale = originalScale;

        RestoreRenderOrder();
    }

    public CardSlot GetPlacedSlot()
    {
        return placedSlot;
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

        Vector3 placedTargetScale =
            originalScale
            * GetPlacedScaleMultiplier();

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
                placedTargetScale,
                t
            );

            yield return null;
        }

        transform.position = targetPos;

        transform.rotation = targetRot;

        transform.localScale = placedTargetScale;

        targetPosition = targetPos;

        targetRotation = targetRot;

        targetScale = placedTargetScale;
    }

    // -------------------------------------------------------
    // 호버
    // -------------------------------------------------------
    private void HandleHoverInput()
    {
        if (isDragging || isPlaced)
            return;

        if (currentHoveredCard != this)
            return;

        if (pointerPressedThisFrame)
        {
            isMouseDown = true;

            mouseDownTimer = 0f;

            mouseDownPosition = pointerPosition;
        }

        // 누르고 있는 중
        if (isMouseDown && pointerIsPressed)
        {
            mouseDownTimer += Time.deltaTime;

            float distance =
                Vector2.Distance(
                    mouseDownPosition,
                    pointerPosition
                );

            // 시간 + 거리 둘다 만족
            if (
                distance >= dragThreshold &&
                mouseDownTimer >= dragHoldTime
            )
            {
                isMouseDown = false;

                StartDrag();
            }
        }

        // 마우스 떼면 초기화
        if (pointerReleasedThisFrame)
        {
            isMouseDown = false;

            mouseDownTimer = 0f;
        }
    }

    private void HandlePlacedClick()
    {
        if (
            CardSystemManager.Instance == null ||
            !CardSystemManager.Instance.IsTurnActive ||
            Mouse.current == null ||
            Camera.main == null ||
            !Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        int hitCount =
            Physics.RaycastNonAlloc(ray, hoverHits);

        CardObject clickedCard =
            FindClosestCardInHits(hitCount);

        if (clickedCard != this)
            return;

        CardSystemManager.Instance
            .ReturnPlacedCardToHand(this);
    }

    private static CardObject FindClosestCardInHits(
        int hitCount
    )
    {
        CardObject closestCard = null;

        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            CardObject card = hoverHits[i]
                .collider
                .GetComponentInParent<CardObject>();

            if (card == null)
                continue;

            if (hoverHits[i].distance < closestDistance)
            {
                closestDistance = hoverHits[i].distance;

                closestCard = card;
            }
        }

        return closestCard;
    }

    private static void UpdateHoverState()
    {
        if (lastHoverUpdateFrame == Time.frameCount)
            return;

        lastHoverUpdateFrame = Time.frameCount;

        if (Mouse.current == null || Camera.main == null)
        {
            SetHoveredCard(null);
            return;
        }

        pointerPosition =
            Mouse.current.position.ReadValue();

        pointerPressedThisFrame =
            Mouse.current.leftButton.wasPressedThisFrame;

        pointerReleasedThisFrame =
            Mouse.current.leftButton.wasReleasedThisFrame;

        pointerIsPressed =
            Mouse.current.leftButton.isPressed;

        Ray ray =
            Camera.main.ScreenPointToRay(pointerPosition);

        int hitCount =
            Physics.RaycastNonAlloc(ray, hoverHits);

        CardObject hoveredCard =
            FindClosestHoverCard(hitCount);

        SetHoveredCard(hoveredCard);
    }

    private static CardObject FindClosestHoverCard(
        int hitCount
    )
    {
        CardObject closestHandCard = null;

        CardObject closestPlacedCard = null;

        float closestHandDistance = float.MaxValue;

        float closestPlacedDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            CardObject card = hoverHits[i]
                .collider
                .GetComponentInParent<CardObject>();

            if (card == null || !card.CanHover())
                continue;

            if (!card.isPlaced)
            {
                if (hoverHits[i].distance < closestHandDistance)
                {
                    closestHandDistance = hoverHits[i].distance;

                    closestHandCard = card;
                }

                continue;
            }

            if (hoverHits[i].distance < closestPlacedDistance)
            {
                closestPlacedDistance = hoverHits[i].distance;

                closestPlacedCard = card;
            }
        }

        return closestHandCard != null
            ? closestHandCard
            : closestPlacedCard;
    }

    private bool CanHover()
    {
        return isActiveAndEnabled &&
            !isDragging;
    }

    private static void SetHoveredCard(
        CardObject nextCard
    )
    {
        if (currentHoveredCard == nextCard)
            return;

        if (currentHoveredCard != null)
        {
            currentHoveredCard.HoverExit();
        }

        if (nextCard != null)
        {
            nextCard.HoverEnter();
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

        targetPosition = GetHoverPosition();

        targetRotation = GetHoverRotation();

        targetScale =
            GetRestScale() * hoverScaleMultiplier;

        BoostRenderOrder();

        CardTooltipUI.GetOrCreate()
            .Show(data);
    }

    private void HoverExit()
    {
        if (isDragging)
            return;

        isMouseDown = false;

        mouseDownTimer = 0f;

        targetPosition = GetRestPosition();

        targetScale = GetRestScale();

        targetRotation = GetRestRotation();

        if (currentHoveredCard == this)
        {
            currentHoveredCard = null;
            CardTooltipUI.Instance?.Hide();
        }

        RestoreRenderOrder();

        CardTooltipUI.Instance?.Hide();
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

        isMouseDown = false;

        mouseDownTimer = 0f;

        targetScale = originalScale;

        if (currentHoveredCard == this)
        {
            currentHoveredCard = null;
        }

        BoostRenderOrder();

        CardTooltipUI.Instance?.Hide();

        if (data != null)
        {
            SlotGuideManager.GetOrCreate()
                .ShowGuidesForCard(this);
        }

        Vector3 liftedDragPosition = transform.position;

        liftedDragPosition.y =
            originPosition.y
            + dragHeight;

        transform.position = liftedDragPosition;

        targetPosition = liftedDragPosition;

        dragStartWorldPos =
            transform.position;

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
                + dragHeight;

            targetPosition = target;
        }
    }

    // -------------------------------------------------------
    // 드래그 종료
    // -------------------------------------------------------
    private void StopDrag()
    {
        isDragging = false;

        if (SlotGuideManager.Instance != null)
        {
            SlotGuideManager.Instance.HideAllGuides();
        }

        float movedDistance =
            Vector3.Distance(
                dragStartWorldPos,
                transform.position
            );

        // 너무 조금 움직였으면 취소
        if (
            movedDistance <
            minDropDistance
        )
        {
            ReturnToOrigin();
            return;
        }

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

            case CardType.Trap:
                targetSlots =
                    CardSystemManager.Instance.trapSlots;
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
        if (SlotGuideManager.Instance != null)
        {
            SlotGuideManager.Instance.HideAllGuides();
        }

        targetPosition = originPosition;

        targetRotation = fanRotation;

        targetScale = originalScale;

        RestoreRenderOrder();
    }

    private Vector3 GetRestPosition()
    {
        return isPlaced
            ? targetPosition
            : originPosition;
    }

    private Vector3 GetHoverPosition()
    {
        Vector3 restPosition = GetRestPosition();

        if (isPlaced)
            return restPosition;

        Camera targetCamera = cam != null
            ? cam
            : Camera.main;

        if (targetCamera == null)
            return restPosition;

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(restPosition);

        screenPosition.y += handHoverScreenLift;

        return targetCamera.ScreenToWorldPoint(screenPosition);
    }

    private Quaternion GetRestRotation()
    {
        return isPlaced
            ? targetRotation
            : fanRotation;
    }

    private Quaternion GetHoverRotation()
    {
        return isPlaced
            ? GetRestRotation()
            : originalRotation;
    }

    private Vector3 GetRestScale()
    {
        return isPlaced
            ? originalScale * GetPlacedScaleMultiplier()
            : originalScale;
    }

    private void CacheRenderers()
    {
        cardRenderers =
            GetComponentsInChildren<Renderer>(true);

        originalSortingOrders =
            new int[cardRenderers.Length];

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            originalSortingOrders[i] =
                cardRenderers[i].sortingOrder;
        }
    }

    private void BoostRenderOrder()
    {
        if (isRenderOrderBoosted)
            return;

        if (cardRenderers == null)
        {
            CacheRenderers();
        }

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            cardRenderers[i].sortingOrder =
                originalSortingOrders[i]
                + hoverSortingOrderBoost;
        }

        isRenderOrderBoosted = true;
    }

    private void RestoreRenderOrder()
    {
        if (!isRenderOrderBoosted)
            return;

        if (
            cardRenderers == null ||
            originalSortingOrders == null
        )
        {
            isRenderOrderBoosted = false;
            return;
        }

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            if (cardRenderers[i] == null)
                continue;

            cardRenderers[i].sortingOrder =
                originalSortingOrders[i];
        }

        isRenderOrderBoosted = false;
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
    public float GetPlacedScaleMultiplier()
    {
        if (data == null)
            return placedScaleMultiplier;

        switch (data.cardType)
        {
            case CardType.Character:
                return characterPlacedScale;

            case CardType.Buff:
                return buffPlacedScale;

            case CardType.Trap:
                return trapPlacedScale;

            default:
                return placedScaleMultiplier;
        }
    }

    public Vector2 GetPlacedGuideSize()
    {
        Collider cardCollider = GetComponent<Collider>();

        float placedScale = GetPlacedScaleMultiplier();

        if (cardCollider is BoxCollider boxCollider)
        {
            Vector3 colliderSize = boxCollider.size;

            return new Vector2(
                colliderSize.x
                    * Mathf.Abs(originalScale.x)
                    * placedScale,
                colliderSize.y
                    * Mathf.Abs(originalScale.y)
                    * placedScale
            );
        }

        SpriteRenderer spriteRenderer =
            cardFrontRenderer != null
                ? cardFrontRenderer
                : GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds spriteBounds = spriteRenderer.sprite.bounds;

            return new Vector2(
                spriteBounds.size.x
                    * Mathf.Abs(originalScale.x)
                    * placedScale,
                spriteBounds.size.y
                    * Mathf.Abs(originalScale.y)
                    * placedScale
            );
        }

        return Vector2.one * placedScale;
    }

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
