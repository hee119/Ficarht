using UnityEngine;

public class LavaDamageTrigger : MonoBehaviour
{
    public LavaMapManager lavaManager;

    private void OnTriggerEnter(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null) return;
        lavaManager.PlayerEnterLava(stat);
    }

    private void OnTriggerExit(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null) return;
        lavaManager.PlayerExitLava(stat);
    }
}