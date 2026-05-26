using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class AnimManager : MonoBehaviour
{
    [Header("Anim List")]
    public List<string> AnimatorState = new List<string>();

    [Header("Key List")]
    public List<string> KeyList = new List<string>();

    private Animator animator;

    private bool isRightAttack;

    [SerializeField]
    private CharactorType charactorType;

    int currentLayer = 1;

    public enum CharactorType
    {
        Mage,
        Paladin,
        Berserker,
        Bard
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        for (int i = 0; i < animator.layerCount; i++)
        {
            Debug.Log("Layer " + i + ": " + animator.GetLayerName(i));
        }
    }

    public void OnKey(InputAction.CallbackContext context)
    {
        if (charactorType == CharactorType.Paladin && context.canceled)
        {
            animator.SetBool("Defense", false);
        }

        if (!context.started)
            return;

        Debug.Log(context.control.name);

        for (int i = 0; i < KeyList.Count; i++)
        {
            if (context.control.name == KeyList[i] &&
                animator.GetCurrentAnimatorStateInfo(currentLayer).normalizedTime >= 1.0f)
            {
                Anim(i);
                break;
            }
        }
    }

    public void Anim(int i)
    {

        for (currentLayer = 1; currentLayer < animator.layerCount; currentLayer++)
        {
            if (animator.HasState(currentLayer, Animator.StringToHash(AnimatorState[i])))
            {
                animator.Play(AnimatorState[i], currentLayer, 0f);
                return;
            }
        }
    }

    public void GetEffect(string effectName)
    {
        Debug.Log(effectName);
        PoolManager.Instance.GetPrefab(effectName, transform);
    }
}