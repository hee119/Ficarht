using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

public class CardSystemManager : MonoBehaviour
{
    public static CardSystemManager Instance { get; private set; }

    [Header("--- 덱 ---")]
    public List<CardData> characterDeck =
        new List<CardData>();

    public List<CardData> buffDeck =
        new List<CardData>();

    public List<CardData> skillDeck =
        new List<CardData>();

    [Header("--- 드로우 포지션 (월드) ---")]
    public List<Transform> characterDrawPositions =
        new List<Transform>();

    public List<Transform> buffDrawPositions =
        new List<Transform>();

    public List<Transform> skillDrawPositions =
        new List<Transform>();

    [Header("--- 슬롯 ---")]
    public List<CardSlot> characterSlots =
        new List<CardSlot>();

    public List<CardSlot> buffSlots =
        new List<CardSlot>();

    public List<CardSlot> skillSlots =
        new List<CardSlot>();

    [Header("--- UI ---")]
    public TextMeshProUGUI timerText;

    [Header("--- 타이머 ---")]
    public float turnDuration = 60f;

    public bool IsTurnActive => isTurnActive;

    public List<CardObject> playerHand =
        new List<CardObject>();

    private bool isTurnActive = false;

    private float turnTimer = 0f;

    private RuntimeStats myStats = null;

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
                StartGame();
            }
        }
    }

    // -------------------------------------------------------
    // 게임 시작
    // -------------------------------------------------------
    public void StartGame()
    {
        
        CheckAllCardPrefabs();

        Debug.Log("[CardSystemManager] 게임 시작!");

        ClearAll();
        
        Debug.Log("[CardSystemManager] 게임 시작!");

        ClearAll();

        // 캐릭터 1장
        DrawCards(
            characterDeck,
            1,
            characterDrawPositions
        );

        // 버프 3장
        DrawCards(
            buffDeck,
            3,
            buffDrawPositions
        );

        // 스킬 2장
        DrawCards(
            skillDeck,
            2,
            skillDrawPositions
        );

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

        List<CardData> availableCards =
            new List<CardData>();

        foreach (var card in deck)
        {
            if (card != null)
                availableCards.Add(card);
        }

        if (availableCards.Count == 0)
        {
            Debug.LogWarning("유효한 카드가 없습니다.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (availableCards.Count == 0)
            {
                Debug.LogWarning(
                    $"카드 부족! 요청:{count}"
                );
                break;
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

            Debug.Log(
                $"[CardSystemManager] '{data.cardName}' 드로우"
            );
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

        Debug.Log(
            $"[CardSystemManager] '{card.data.cardName}' 배치됨. 남은 핸드: {playerHand.Count}장"
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

        SetTimerText(
            $"남은 시간: {Mathf.CeilToInt(turnTimer)}초"
        );

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
        isTurnActive = false;

        SetTimerText("시간 종료!");

        myStats = GetCharacterStats();

        if (myStats != null)
        {
            BuffApplier.ApplyAll(
                buffSlots,
                myStats
            );

            Debug.Log(
                $"[CardSystemManager] 버프 적용 후 스탯: {myStats}"
            );
        }

        SkillRegistry.Instance
            ?.RegisterFromSlots(skillSlots);

        CardRevealSystem.Instance
            ?.RevealAllCards();

        FinalizeCards();
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
        allSlots.AddRange(skillSlots);

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

        List<CardSlot> allSlots =
            new List<CardSlot>();

        allSlots.AddRange(characterSlots);
        allSlots.AddRange(buffSlots);
        allSlots.AddRange(skillSlots);

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
    // 타이머 텍스트
    // -------------------------------------------------------
    private void SetTimerText(string msg)
    {
        if (timerText != null)
        {
            timerText.text = msg;
        }
    }
    
    private void CheckAllCardPrefabs()
    {
        Debug.Log("===== 카드 프리팹 검사 시작 =====");

        CheckDeck(characterDeck);
        CheckDeck(buffDeck);
        CheckDeck(skillDeck);

        Debug.Log("===== 카드 프리팹 검사 종료 =====");
    }

    private void CheckDeck(List<CardData> deck)
    {
        foreach (var card in deck)
        {
            if (card == null)
            {
                Debug.LogError(
                    "[카드 데이터 누락] CardData가 비어있음"
                );

                continue;
            }

            if (card.cardPrefab == null)
            {
                Debug.LogError(
                    $"[프리팹 누락] 이름:{card.cardName} 타입:{card.cardType} 에셋:{card.name}"
                );
            }
            else
            {
                Debug.Log(
                    $"[정상] 이름:{card.cardName} → {card.cardPrefab.name}"
                );
            }
        }
    }
}