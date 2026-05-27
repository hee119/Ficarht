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
    
    ICharacterSkill characterSkill;

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
        characterSkill = GetComponent<ICharacterSkill>();
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
            {
                if (context.control.name == KeyList[i] &&
                    animator.GetCurrentAnimatorStateInfo(currentLayer).normalizedTime >= 0.95f)
                {

                    Anim(i);
                    break;
                }
            }
        }
    }

    public void Anim(int i)
    {
        string stateName = AnimatorState[i];
        int stateHash = Animator.StringToHash(stateName);
        bool played = false;

        // 애니메이터에 설정된 모든 레이어를 검사 (0부터 끝까지)
        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            // 해당 레이어에 stateName이 있는지 확인
            if (animator.HasState(layerIndex, stateHash))
            {
                animator.Play(stateHash, layerIndex, 0f);
                Debug.Log($"[재생 성공] 레이어 {layerIndex}에서 '{stateName}' 재생 시작");
                played = true;
                break; // 찾았으면 다른 레이어 검사를 중단하고 나감
            }
        }

        if (!played)
        {
            Debug.LogError($"[재생 실패] '{stateName}' 상태를 모든 레이어({animator.layerCount}개)에서 찾을 수 없습니다.");
        }
    }
    
    public void GetEffect(string effectName)
    {
        characterSkill.UseSkill(effectName, transform);
    }
}