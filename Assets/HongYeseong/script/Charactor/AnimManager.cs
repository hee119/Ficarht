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

    [SerializeField]
    private CharactorType charactorType;

    private CoolTime coolTime;

    private bool isTriggerPlaying = false;
    
    public PlayerController playerController;
    
    CharaStat charaStat;

    public enum CharactorType
    {
        Mage,
        Paladin,
        Berserker,
        Bard
    }

    private void Awake()
    {
        coolTime = GetComponent<CoolTime>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        charaStat = GetComponent<CharaStat>();
    }

    public void OnKey(InputAction.CallbackContext context)
    {
        for (int i = 0; i < KeyList.Count; i++)
        {
            if (context.control.name != KeyList[i])
                continue;

            string animName = AnimatorState[i];

            if (context.started)
            {
                if (IsTrigger(animName))
                {
                    // 다른 트리거 애니메이션 재생 중이면 무시
                    if (isTriggerPlaying || !coolTime.CoolTimeCheck(animName))
                    {
                        break;
                    }

                    isTriggerPlaying = true;
                    animator.SetTrigger(animName);
                    if(animName == "Roll")
                    {
                        playerController.Roll();
                    }
                    break;
                }

                if (IsBool(animName))
                {
                    if (isTriggerPlaying)
                    {
                        break;
                    }
                    animator.SetBool(animName, true);
                    break;
                }
                
                if (animName == "1Hand_Up_Shield_Block_Idle_1")
                {
                    animator.SetBool(animName, true);

                    charaStat.isBlocking = true;
                }
            }

            if (context.canceled)
            {
                if (IsBool(animName))
                {
                    animator.SetBool(animName, false);
                    break;
                }
                charaStat.isBlocking = false;
            }

            break;
        }
    }

    bool IsTrigger(string parameterName)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];

            if (parameter.name == parameterName &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    bool IsBool(string parameterName)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];

            if (parameter.name == parameterName &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                return true;
            }
        }

        return false;
    }

    public void GetEffect(string effectName)
    {
        coolTime?.UseSkill(effectName, transform);
    }

    public void SetBool()
    {
        isTriggerPlaying = false;
        playerController.isAttacking  = false;
        playerController.isUsingSkill = false;
    }
}
