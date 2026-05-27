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
    
    // 1. 데이터가 잘 들어오는지 확인
    Debug.Log($"재생 시도: {stateName} (인덱스: {i})");

    // 2. 0번 레이어(Base Layer)에서 강제 재생 시도
    animator.Play(stateName, 1, 0f);

    // 3. 실제로 해당 이름의 상태가 애니메이터에 있는지 검증
    bool hasState = animator.HasState(1, Animator.StringToHash(stateName));
    if(!hasState)
    {
        Debug.LogError($"{stateName} 이라는 이름의 상태가 1번 레이어에 없습니다! 이름을 다시 확인하세요.");
    }
}
    
    public void GetEffect(string effectName)
    {
        characterSkill.UseSkill(effectName, transform);
    }
}