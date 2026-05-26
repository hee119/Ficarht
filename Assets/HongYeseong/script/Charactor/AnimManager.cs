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
    }

    public void OnKey(InputAction.CallbackContext context)
    {
        if (!context.started)
            return;

        Debug.Log(context.control.name);

        for (int i = 0; i < KeyList.Count; i++)
        {
            if (context.control.name == KeyList[i] &&
                animator.GetCurrentAnimatorStateInfo(1).normalizedTime >= 1.0f)
            {
                Anim(i);
                break;
            }
        }
    }

    public void Anim(int i)
    {
        animator.Play(AnimatorState[i], 1, 0f);
    }
}