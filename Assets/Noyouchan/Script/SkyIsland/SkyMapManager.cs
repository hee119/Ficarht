using UnityEngine;

public class SkyMapManager : MonoBehaviour
{
    [Tooltip("이 Y값 이하로 떨어지면 사망")]
    public float deathY = 0f;

    private CharaStat stat;

    private void Awake()
    {
        stat = GetComponent<CharaStat>();

        if (stat == null)
            Debug.LogError($"{name} : CharaStat이 NULL입니다.");
    }

    private void Update()
    {
        if (stat == null) return;

        if (transform.position.y <= deathY)
        {
            Debug.Log("사망 조건 충족");
            Die();
        }
    }

    private void Die()
    {
        stat.isShield = false;
        stat.Hit(1900000f);
    }
}