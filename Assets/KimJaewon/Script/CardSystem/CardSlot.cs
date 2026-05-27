using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CardSlot : MonoBehaviour
{
    [Header("슬롯 설정")]
    public CardType allowedType;

    public CardObject currentCard;

    public Transform snapPoint;

    private void Awake()
    {
        Collider col =
            GetComponent<Collider>();

        if (!col.isTrigger)
        {
            col.isTrigger = true;

            Debug.LogWarning(
                $"[CardSlot] {gameObject.name} Collider를 isTrigger=true로 설정했습니다."
            );
        }
    }

    public bool TryPlaceCard(
        CardObject card
    )
    {
        // 타입 검사
        if (
            card.data.cardType
            != allowedType
        )
        {
            Debug.LogWarning(
                $"[CardSlot] 타입 불일치: 슬롯={allowedType}, 카드={card.data.cardType}"
            );

            return false;
        }

        // 이미 사용중
        if (currentCard != null)
        {
            Debug.LogWarning(
                $"[CardSlot] {gameObject.name} 슬롯이 이미 사용 중입니다."
            );

            return false;
        }

        currentCard = card;

        // 스냅 위치
        Transform target =
            snapPoint != null
            ? snapPoint
            : transform;

        // 카드 이동
        card.PlaceToSlot(
            target.position,
            target.rotation
        );

        // 카드 시스템 알림
        if (
            CardSystemManager.Instance
            != null
        )
        {
            CardSystemManager.Instance
                .OnCardPlaced(card);
        }

        Debug.Log(
            $"[CardSlot] {allowedType} 슬롯에 '{card.data.cardName}' 배치 성공!"
        );

        return true;
    }

    public void ClearSlot()
    {
        if (currentCard != null)
        {
            Destroy(
                currentCard.gameObject
            );

            currentCard = null;
        }
    }

    public CardObject PopCard()
    {
        CardObject card =
            currentCard;

        currentCard = null;

        if (card != null)
        {
            card.transform.SetParent(
                null
            );
        }

        return card;
    }
}