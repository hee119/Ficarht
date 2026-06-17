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

    private ICharacterSkill characterSkill;

    private bool isTriggerPlaying = false;

    public enum CharactorType
    {
        Mage,
        Paladin,
        Berserker,
        Bard
    }

    private void Awake()
    {
        characterSkill = GetComponent<ICharacterSkill>();
        animator = GetComponent<Animator>();

        for (int i = 0; i < animator.layerCount; i++)
        {
            Debug.Log("Layer " + i + ": " + animator.GetLayerName(i));
        }
    }

    public void OnKey(InputAction.CallbackContext context)
    {
        Debug.Log(
            $"Action : {context.action.name}, Control : {context.control.name}, phase : {context.phase}"
        );
        Debug.Log(context.control.name);

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
                    if (isTriggerPlaying)
                    {
                        Debug.Log("[Trigger 차단] 현재 다른 트리거 애니메이션 재생 중");
                        break;
                    }

                    isTriggerPlaying = true;
                    animator.SetTrigger(animName);

                    Debug.Log($"[Trigger 재생] '{animName}'");
                    break;
                }

                if (IsBool(animName))
                {
                    if (isTriggerPlaying)
                    {
                        Debug.Log("[Trigger 차단] 현재 다른 트리거 애니메이션 재생 중");
                        break;
                    }
                    animator.SetBool(animName, true);
                    Debug.Log($"[Bool 변경] '{animName}' = true");
                    break;
                }
            }

            if (context.canceled)
            {
                if (IsBool(animName))
                {
                    animator.SetBool(animName, false);
                    Debug.Log($"[Bool 변경] '{animName}' = false");
                    break;
                }
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
        characterSkill?.UseSkill(effectName, transform);
    }

    public void SetBool()
    {
        isTriggerPlaying = false;
    }
}