using System;
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
        coolTime         = GetComponent<CoolTime>();
        animator         = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        charaStat        = GetComponent<CharaStat>();
    }

    // ─────────────────────────────────────────────
    // Update: Keyboard.current 직접 폴링
    // InputAction 콜백 방식은 Shift 등 모디파이어 키를 누른 채로
    // 다른 키를 입력하면 이벤트가 막히는 문제가 있음.
    // ─────────────────────────────────────────────
    void Update()
    {
        if (charaStat == null || charaStat.playerInput == null) return;
        if (!charaStat.playerInput.enabled) return;

        for (int i = 0; i < KeyList.Count; i++)
        {
            string key      = KeyList[i];
            string animName = AnimatorState[i];

            if (IsKeyDown(key))
                HandleKeyStarted(animName);

            if (IsKeyUp(key))
                HandleKeyCanceled(animName);
        }
    }

    void HandleKeyStarted(string animName)
    {
        if (IsTrigger(animName))
        {
            float staminaCost = coolTime.GetStaminaCost(animName);

            if (staminaCost > 0f && charaStat.stamina < staminaCost)
            {
                Debug.Log($"{animName} : 스태미너 부족 ({charaStat.stamina:F1} / {staminaCost})");
                return;
            }

            if (isTriggerPlaying || !coolTime.CoolTimeCheck(animName))
                return;

            if (staminaCost > 0f)
            {
                charaStat.UseStamina(staminaCost);
                Debug.Log($"{animName} 스태미너 -{staminaCost} → 잔여 {charaStat.stamina:F1}");
            }

            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            isTriggerPlaying             = true;
            playerController.isAttacking = true;
            animator.CrossFadeInFixedTime(animName, 0.1f);
            playerController.BroadcastAnimTrigger(animName);

            if (animName == "Roll") playerController.Roll();
            return;
        }

        if (IsBool(animName))
        {
            if (isTriggerPlaying) return;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
            playerController.isAttacking = true;
            animator.SetBool(animName, true);
            playerController.BroadcastAnimBool(animName, true);
            return;
        }

        if (animName == "1Hand_Up_Shield_Block_Idle_1")
        {
            animator.SetBool(animName, true);
            playerController.BroadcastAnimBool(animName, true);
            charaStat.isBlocking = true;
        }
    }

    void HandleKeyCanceled(string animName)
    {
        if (IsBool(animName))
        {
            animator.SetBool(animName, false);
            playerController.isAttacking = false;
            playerController.BroadcastAnimBool(animName, false);
            return;
        }
        charaStat.isBlocking = false;
    }

    // ─────────────────────────────────────────────
    // 키 이름 → 이번 프레임 눌림/뗌 여부
    // ─────────────────────────────────────────────
    static bool IsKeyDown(string keyName)
    {
        switch (keyName)
        {
            case "leftButton":   return Mouse.current?.leftButton.wasPressedThisFrame   ?? false;
            case "rightButton":  return Mouse.current?.rightButton.wasPressedThisFrame  ?? false;
            case "middleButton": return Mouse.current?.middleButton.wasPressedThisFrame ?? false;
        }
        if (Enum.TryParse<Key>(keyName, true, out Key k))
            return Keyboard.current?[k].wasPressedThisFrame ?? false;
        return false;
    }

    static bool IsKeyUp(string keyName)
    {
        switch (keyName)
        {
            case "leftButton":   return Mouse.current?.leftButton.wasReleasedThisFrame   ?? false;
            case "rightButton":  return Mouse.current?.rightButton.wasReleasedThisFrame  ?? false;
            case "middleButton": return Mouse.current?.middleButton.wasReleasedThisFrame ?? false;
        }
        if (Enum.TryParse<Key>(keyName, true, out Key k))
            return Keyboard.current?[k].wasReleasedThisFrame ?? false;
        return false;
    }

    // ─────────────────────────────────────────────
    // PlayerInput 바인딩 유지용 스텁 (더 이상 실제 처리 안 함)
    // ─────────────────────────────────────────────
    public void OnKey(InputAction.CallbackContext context) { }

    bool IsTrigger(string parameterName)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.parameters[i];
            if (p.name == parameterName && p.type == AnimatorControllerParameterType.Trigger)
                return true;
        }
        return false;
    }

    bool IsBool(string parameterName)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.parameters[i];
            if (p.name == parameterName && p.type == AnimatorControllerParameterType.Bool)
                return true;
        }
        return false;
    }

    public void GetEffect(string effectName)
    {
        coolTime?.UseSkill(effectName, transform);
    }

    public void SetBool()
    {
        isTriggerPlaying             = false;
        playerController.isAttacking = false;
    }
}
