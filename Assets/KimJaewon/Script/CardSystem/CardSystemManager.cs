using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using MTextButton = TinyGiantStudio.Text.Button;

public class CardSystemManager : MonoBehaviour
{
    public static CardSystemManager Instance { get; private set; }

    [Header("--- 덱 ---")]
    public List<CardData> characterDeck =
        new List<CardData>();

    public List<CardData> buffDeck =
        new List<CardData>();

    [FormerlySerializedAs("skillDeck")]
    public List<CardData> trapDeck =
        new List<CardData>();

    public List<CardData> mapDeck =
        new List<CardData>();

    [Header("--- 드로우 포지션 (월드) ---")]
    public List<Transform> characterDrawPositions =
        new List<Transform>();

    public List<Transform> buffDrawPositions =
        new List<Transform>();

    [FormerlySerializedAs("skillDrawPositions")]
    public List<Transform> trapDrawPositions =
        new List<Transform>();

    public List<Transform> mapDrawPositions =
        new List<Transform>();

    [Header("--- 슬롯 ---")]
    public List<CardSlot> characterSlots =
        new List<CardSlot>();

    public List<CardSlot> buffSlots =
        new List<CardSlot>();

    [FormerlySerializedAs("skillSlots")]
    public List<CardSlot> trapSlots =
        new List<CardSlot>();

    public List<CardSlot> mapSlots =
        new List<CardSlot>();

    [Header("--- UI ---")]
    public TextMeshProUGUI timerText;

    [Header("--- Collect 버튼 ---")]
    public string collectButtonName = "Collect_Button";

    [Header("--- 선택 완료 조건 ---")]
    public int requiredCharacterCards = 1;

    public int requiredBuffCards = 2;

    public int requiredTrapCards = 2;

    public float selectionMessageDuration = 1.5f;

    [Header("--- 타이머 ---")]
    public float turnDuration = 60f;

    public bool IsTurnActive => isTurnActive;

    public List<CardObject> playerHand =
        new List<CardObject>();

    private readonly List<CardObject> drawOrder =
        new List<CardObject>();

    private bool isTurnActive = false;

    private float turnTimer = 0f;

    private float selectionMessageTimer = 0f;

    private RuntimeStats myStats = null;

    private int lastCollectFrame = -1;

    private MTextButton collectButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        BindCollectButton();

        SetTimerText("3D 박스를 클릭하세요!");
    }

    private void Update()
    {
        HandleCardBoxClick();
        HandleTimer();
    }

    // -------------------------------------------------------
    // 카드박스 클릭
    // -------------------------------------------------------
    private void HandleCardBoxClick()
    {
        if (isTurnActive)
            return;

        if (Pointer.current == null)
            return;

        if (!Pointer.current.press.wasPressedThisFrame)
            return;

        Ray ray = Camera.main.ScreenPointToRay(
            Pointer.current.position.ReadValue()
        );

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (
                hit.collider.CompareTag("CardBox") ||
                hit.collider.name == "CardBox"
            )
            {
                // 멀티: 서버에 시작 요청 → RpcStartCards → StartGameExternal()
                // 싱글(로컬 테스트): 바로 StartGame()
                if (NetworkCardBridge.LocalInstance != null)
                    NetworkCardBridge.LocalInstance.CmdRequestStartCards();
                else
                    StartGame();
            }
        }
    }

    // -------------------------------------------------------
    // 게임 시작
    // -------------------------------------------------------
    public void StartGame()
    {
        ClearAll();

        // 캐릭터 1장
        DrawCards(
            characterDeck,
            1,
            characterDrawPositions
        );

        // 버프 2장
        DrawCards(
            buffDeck,
            2,
            buffDrawPositions
        );

        // 함정 2장
        DrawCards(
            trapDeck,
            2,
            trapDrawPositions
        );

        // 맵 1장 (덱과 포지션이 모두 설정된 경우만)
        if (mapDeck != null && mapDeck.Count > 0
            && mapDrawPositions != null && mapDrawPositions.Count > 0)
        {
            DrawCards(
                mapDeck,
                1,
                mapDrawPositions
            );
        }

        // 부채꼴 정렬
        HandLayoutManager.Instance
            ?.ArrangeHand(playerHand);

        CardRevealSystem.Instance
            ?.HideOpponentCards();

        turnTimer = turnDuration;

        isTurnActive = true;
    }

    // -------------------------------------------------------
    // 카드 드로우
    // -------------------------------------------------------
    private void DrawCards(
        List<CardData> deck,
        int count,
        List<Transform> positions
    )
    {
        if (deck == null || deck.Count == 0)
        {
            Debug.LogWarning("덱이 비어있습니다.");
            return;
        }

        if (positions == null || positions.Count == 0)
        {
            Debug.LogWarning("드로우 포지션이 비어있습니다. Inspector에서 연결 확인.");
            return;
        }

        List<CardData> availableCards =
            new List<CardData>();

        List<CardData> validCards =
            new List<CardData>();

        foreach (var card in deck)
        {
            if (card != null)
            {
                validCards.Add(card);
            }
        }

        if (validCards.Count == 0)
        {
            Debug.LogWarning("유효한 카드가 없습니다.");
            return;
        }

        availableCards.AddRange(validCards);

        for (int i = 0; i < count; i++)
        {
            if (availableCards.Count == 0)
            {
                Debug.LogWarning(
                    $"카드 종류 부족! 요청:{count}, 덱:{validCards.Count}. 같은 카드가 다시 나올 수 있습니다."
                );

                availableCards.AddRange(validCards);
            }

            Transform spawnTf =
                positions[
                    Mathf.Min(
                        i,
                        positions.Count - 1
                    )
                ];

            int randomIndex =
                Random.Range(
                    0,
                    availableCards.Count
                );

            CardData data =
                availableCards[randomIndex];

            availableCards.RemoveAt(randomIndex);

            if (data.cardPrefab == null)
            {
                Debug.LogError(
                    $"[프리팹 누락] {data.cardName}"
                );
                continue;
            }

            GameObject go = Instantiate(
                data.cardPrefab,
                spawnTf.position,
                spawnTf.rotation
            );

            CardObject card =
                go.GetComponent<CardObject>();

            if (card == null)
            {
                Debug.LogError(
                    $"{data.cardName} 프리팹에 CardObject가 없습니다."
                );
                Destroy(go);
                continue;
            }

            card.SetVisible(false);
            card.Setup(data);

            playerHand.Add(card);

            drawOrder.Add(card);

        }
    }

    // -------------------------------------------------------
    // 카드 배치 완료
    // -------------------------------------------------------
    public void OnCardPlaced(CardObject card)
    {
        if (card == null)
            return;

        playerHand.Remove(card);

        HandLayoutManager.Instance
            ?.ReArrange(playerHand);

    }

    public void ReturnPlacedCardToHand(CardObject card)
    {
        if (card == null || !isTurnActive)
            return;

        CardSlot slot = card.GetPlacedSlot();

        if (slot == null || !slot.TryRemoveCard(card))
            return;

        card.ReturnToHand();

        if (!playerHand.Contains(card))
        {
            playerHand.Add(card);
        }

        SortHandByDrawOrder();

        HandLayoutManager.Instance
            ?.ReArrange(playerHand);

    }

    private void BindCollectButton()
    {
        if (string.IsNullOrWhiteSpace(collectButtonName))
            return;

        GameObject collectButtonObject =
            GameObject.Find(collectButtonName);

        if (collectButtonObject == null)
        {
            Debug.LogWarning(
                $"[CardSystemManager] '{collectButtonName}' 버튼을 찾지 못했습니다."
            );

            return;
        }

        collectButton =
            collectButtonObject.GetComponent<MTextButton>();

        if (collectButton == null)
        {
            Debug.LogWarning(
                $"[CardSystemManager] '{collectButtonName}'에 M3D Button 컴포넌트가 없습니다."
            );

            return;
        }

        collectButton.pressCompleteEvent
            .RemoveListener(CollectPlacedCards);

        collectButton.pressCompleteEvent
            .AddListener(CollectPlacedCards);
    }

    public void CollectPlacedCards()
    {
        if (lastCollectFrame == Time.frameCount)
            return;

        lastCollectFrame = Time.frameCount;

        if (!isTurnActive)
        {
            ShowSelectionMessage(
                "카드를 먼저 뽑으세요."
            );

            return;
        }

        int collectedCount = 0;

        collectedCount +=
            CollectCardsFromSlots(characterSlots);

        collectedCount +=
            CollectCardsFromSlots(buffSlots);

        collectedCount +=
            CollectCardsFromSlots(trapSlots);

        if (collectedCount == 0)
        {
            ShowSelectionMessage(
                "회수할 카드가 없습니다."
            );

            return;
        }

        SortHandByDrawOrder();

        HandLayoutManager.Instance
            ?.ReArrange(playerHand);

    }

    private void SortHandByDrawOrder()
    {
        playerHand.RemoveAll(card => card == null);

        playerHand.Sort(
            (left, right) =>
                GetDrawOrderIndex(left)
                .CompareTo(GetDrawOrderIndex(right))
        );
    }

    private int GetDrawOrderIndex(
        CardObject card
    )
    {
        int index = drawOrder.IndexOf(card);

        return index >= 0
            ? index
            : int.MaxValue;
    }

    private int CollectCardsFromSlots(
        List<CardSlot> slots
    )
    {
        if (slots == null)
            return 0;

        int collectedCount = 0;

        foreach (var slot in slots)
        {
            CardObject card = slot?.currentCard;

            if (card == null)
                continue;

            if (!slot.TryRemoveCard(card))
                continue;

            card.ReturnToHand();

            if (!playerHand.Contains(card))
            {
                playerHand.Add(card);
            }

            collectedCount++;
        }

        return collectedCount;
    }

    public bool IsSelectionComplete()
    {
        if (!isTurnActive)
            return false;

        return
            CountPlacedCards(characterSlots) >=
            requiredCharacterCards &&
            CountPlacedCards(buffSlots) >=
            requiredBuffCards &&
            CountPlacedCards(trapSlots) >=
            requiredTrapCards;
    }

    public bool CanMoveToBattleScene()
    {
        if (!isTurnActive)
        {
            ShowSelectionMessage(
                "카드더미를 클릭해서 카드를 먼저 뽑으세요."
            );

            return false;
        }

        int characterCount =
            CountPlacedCards(characterSlots);

        int buffCount =
            CountPlacedCards(buffSlots);

        int trapCount =
            CountPlacedCards(trapSlots);

        if (characterCount < requiredCharacterCards)
        {
            ShowSelectionMessage(
                $"캐릭터 카드를 {requiredCharacterCards}장 배치해야 합니다. ({characterCount}/{requiredCharacterCards})"
            );

            return false;
        }

        if (buffCount < requiredBuffCards)
        {
            ShowSelectionMessage(
                $"버프 카드를 {requiredBuffCards}장 배치해야 합니다. ({buffCount}/{requiredBuffCards})"
            );

            return false;
        }

        if (trapCount < requiredTrapCards)
        {
            ShowSelectionMessage(
                $"함정 카드를 {requiredTrapCards}장 배치해야 합니다. ({trapCount}/{requiredTrapCards})"
            );

            return false;
        }

        return true;
    }

    private int CountPlacedCards(
        List<CardSlot> slots
    )
    {
        if (slots == null)
            return 0;

        int count = 0;

        foreach (var slot in slots)
        {
            if (slot?.currentCard != null)
            {
                count++;
            }
        }

        return count;
    }

    private void ShowSelectionMessage(
        string message
    )
    {
        selectionMessageTimer =
            selectionMessageDuration;

        SetTimerText(message);

        Debug.LogWarning(
            $"[CardSystemManager] {message}"
        );
    }

    // -------------------------------------------------------
    // 타이머
    // -------------------------------------------------------
    private void HandleTimer()
    {
        if (!isTurnActive)
            return;

        turnTimer -= Time.deltaTime;

        if (selectionMessageTimer > 0f)
        {
            selectionMessageTimer -= Time.deltaTime;
        }
        else
        {
            SetTimerText(
                $"남은 시간: {Mathf.CeilToInt(turnTimer)}초"
            );
        }

        if (turnTimer <= 0f)
        {
            OnTimerEnd();
        }
    }

    // -------------------------------------------------------
    // 턴 종료
    // -------------------------------------------------------
    private void OnTimerEnd()
    {
        if (!isTurnActive) return; // 중복 호출 방지
        isTurnActive = false;

        SetTimerText("시간 종료!");

        myStats = GetCharacterStats();

        if (myStats != null)
        {
            BuffApplier.ApplyAll(
                buffSlots,
                myStats
            );
        }

        CardRevealSystem.Instance
            ?.RevealAllCards();

        FinalizeCards();

        // 멀티: 서버에 카드 선택 결과 전송
        NetworkCardBridge.LocalInstance?.SubmitCardSelection();
    }

    // -------------------------------------------------------
    // 네트워크 전용 메서드
    // -------------------------------------------------------

    /// <summary>서버 RpcStartCards()에서 호출 - 네트워크 시작 신호</summary>
    public void StartGameExternal()
    {
        if (isTurnActive) return;
        StartGame();
    }

    /// <summary>서버 타이머 종료 시 강제 제출 - RpcForceEndCards()에서 호출</summary>
    public void ForceEndTurn()
    {
        if (!isTurnActive) return;
        turnTimer = 0f; // 타이머 강제 종료 → HandleTimer가 OnTimerEnd 호출
    }

    /// <summary>서버에서 5초마다 타이머 보정값 수신</summary>
    public void SyncTimerFromServer(float remaining)
    {
        if (!isTurnActive) return;
        turnTimer = remaining;
    }

    // -------------------------------------------------------
    // 캐릭터 스탯 가져오기
    // -------------------------------------------------------
    private RuntimeStats GetCharacterStats()
    {
        foreach (var slot in characterSlots)
        {
            if (slot?.currentCard == null)
                continue;

            if (
                slot.currentCard.data.cardType
                != CardType.Character
            )
            {
                continue;
            }

            if (
                slot.currentCard.data.characterStats
                == null
            )
            {
                Debug.LogError(
                    $"[CardSystemManager] '{slot.currentCard.data.cardName}'에 CharacterStats가 없습니다!"
                );

                return null;
            }

            return slot
                .currentCard
                .data
                .characterStats
                .CreateRuntime();
        }

        Debug.LogWarning(
            "[CardSystemManager] 배치된 캐릭터 카드가 없습니다."
        );

        return null;
    }

    // -------------------------------------------------------
    // 카드 확정
    // -------------------------------------------------------
    private void FinalizeCards()
    {
        List<CardSlot> allSlots =
            new List<CardSlot>();

        allSlots.AddRange(characterSlots);
        allSlots.AddRange(buffSlots);
        allSlots.AddRange(trapSlots);

        foreach (var slot in allSlots)
        {
            if (slot?.currentCard == null)
                continue;

            Collider col =
                slot.currentCard.GetComponent<Collider>();

            if (col != null)
                col.enabled = false;
        }

        foreach (var card in playerHand)
        {
            if (card == null)
                continue;

            Collider col =
                card.GetComponent<Collider>();

            if (col != null)
                col.enabled = false;
        }
    }

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void ClearAll()
    {
        foreach (var card in playerHand)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }

        playerHand.Clear();

        drawOrder.Clear();

        List<CardSlot> allSlots =
            new List<CardSlot>();

        allSlots.AddRange(characterSlots);
        allSlots.AddRange(buffSlots);
        allSlots.AddRange(trapSlots);

        foreach (var slot in allSlots)
        {
            slot?.ClearSlot();
        }

        SkillRegistry.Instance?.Clear();

        myStats = null;
    }

    // -------------------------------------------------------
    // 최종 스탯 반환
    // -------------------------------------------------------
    public RuntimeStats GetFinalStats()
    {
        return myStats;
    }

    // -------------------------------------------------------
    // 선택된 맵 씬 이름 반환
    // -------------------------------------------------------
    public string GetSelectedMapScene()
    {
        List<CardSlot> allSlots = new List<CardSlot>();
        allSlots.AddRange(mapSlots);
        // mapSlots가 비었으면 playerHand에서도 탐색
        foreach (var card in playerHand)
        {
            if (card?.data?.cardType == CardType.Map &&
                !string.IsNullOrEmpty(card.data.mapSceneName))
                return card.data.mapSceneName;
        }
        foreach (var slot in allSlots)
        {
            if (slot?.currentCard?.data?.cardType == CardType.Map &&
                !string.IsNullOrEmpty(slot.currentCard.data.mapSceneName))
                return slot.currentCard.data.mapSceneName;
        }
        return ""; // 맵 카드 미선택 시 빈 문자열
    }

    // -------------------------------------------------------
    // 타이머 텍스트
    // -------------------------------------------------------
    private void SetTimerText(string msg)
    {
        if (timerText != null)
        {
            timerText.text = msg;
        }
    }
}
