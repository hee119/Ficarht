using Mirror;
using UnityEngine;

public class NetworkAPI : NetworkBehaviour
{
    public static NetworkAPI Instance;

    private void Awake()
    {
        Instance = this;
    }

    // 🔥 데미지 동기화
    public void DealDamage(PlayerNetwork target, int damage)
    {
        if (NetworkClient.active)
        {
            CmdDealDamage(target.netIdentity, damage);
        }
    }

    [Command]
    void CmdDealDamage(NetworkIdentity target, int damage)
    {
        target.GetComponent<PlayerNetwork>().TakeDamage(damage);

        RpcOnDamage(target, damage);
    }

    [ClientRpc]
    void RpcOnDamage(NetworkIdentity target, int damage)
    {
        // 👉 여기서 UI, 이펙트 처리
        Debug.Log($"데미지 {damage}");
    }

    // 🔥 카드 배치
    public void PlaceCard(int cardId, int slot)
    {
        CmdPlaceCard(cardId, slot);
    }

    [Command]
    void CmdPlaceCard(int cardId, int slot)
    {
        RpcPlaceCard(cardId, slot);
    }

    [ClientRpc]
    void RpcPlaceCard(int cardId, int slot)
    {
        Debug.Log($"카드 {cardId} 슬롯 {slot}");
    }
}