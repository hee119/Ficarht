using UnityEngine;
using UnityEngine.UIElements;
using Mirror;

/// <summary>
/// 전투 씬 HUD — 내 HP (왼쪽 아래, 녹색) / 상대방 HP (오른쪽 위, 빨간색).
///
/// 사용법:
///   배틀 씬의 빈 GameObject에 UIDocument + HpBarUI 를 붙인다.
///   UIDocument → Source Asset 에 HpBar.uxml 연결.
///   Panel Settings 는 LoadingScreenUI/MapCardDisplayUI 와 동일한 것 사용.
///
/// 지원 모드:
///   - Mirror 멀티: PlayerNetwork SyncVar(health/maxHealth) 추적
///   - 싱글 플레이: CharaStat.characterStats.health 를 최대값으로 표시
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HpBarUI : MonoBehaviour
{
    public static HpBarUI Instance { get; private set; }

    // ─── UI 요소 ────────────────────────────────────────────
    private VisualElement _myFill;
    private VisualElement _enemyFill;
    private Label _myName;
    private Label _enemyName;
    private Label _myHpText;
    private Label _enemyHpText;

    // ─── 추적 대상 ────────────────────────────────────────────
    private PlayerNetwork _myNet;
    private PlayerNetwork _enemyNet;

    // 싱글 플레이 (Mirror 없음)
    private PlayerController _singlePlayer;
    private float _singleMaxHp = 100f;
    private float _singleHp    = 100f;

    private float _searchTimer = 0f;
    private const float SearchInterval = 0.5f;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _myFill      = root.Q<VisualElement>("my-fill");
        _enemyFill   = root.Q<VisualElement>("enemy-fill");
        _myName      = root.Q<Label>("my-name");
        _enemyName   = root.Q<Label>("enemy-name");
        _myHpText    = root.Q<Label>("my-hp-text");
        _enemyHpText = root.Q<Label>("enemy-hp-text");

        // 시작 시 상대방 패널 숨기기
        var enemyPanel = root.Q<VisualElement>("enemy-panel");
        if (enemyPanel != null)
            enemyPanel.style.display = DisplayStyle.None;
    }

    void Update()
    {
        // 아직 타겟을 못 찾았으면 재탐색
        if (_myNet == null && _singlePlayer == null)
        {
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                _searchTimer = SearchInterval;
                FindTargets();
            }
            return;
        }

        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────
    // 타겟 탐색
    // ─────────────────────────────────────────────────────────
    private void FindTargets()
    {
        if (NetworkClient.active)
        {
            // Mirror 멀티플레이
            var all = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
            foreach (var pn in all)
            {
                if (pn.isOwned) _myNet    = pn;
                else            _enemyNet = pn;
            }

            if (_myNet != null)
            {
                Debug.Log("[HpBarUI] 내 PlayerNetwork 발견");
                SetMyName(GetCharacterName(_myNet));
            }
            if (_enemyNet != null)
            {
                Debug.Log("[HpBarUI] 적 PlayerNetwork 발견");
                SetEnemyName(GetCharacterName(_enemyNet));

                var root = GetComponent<UIDocument>().rootVisualElement;
                var ep   = root.Q<VisualElement>("enemy-panel");
                if (ep != null) ep.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            // 싱글 플레이 — PlayerController 탐색
            _singlePlayer = FindAnyObjectByType<PlayerController>();
            if (_singlePlayer != null)
            {
                CharaStat cs = _singlePlayer.GetComponent<CharaStat>();
                if (cs != null && cs.characterStats != null)
                {
                    _singleMaxHp = cs.characterStats.health;
                    _singleHp    = _singleMaxHp;
                    SetMyName(cs.characterStats.characterName);
                }
                Debug.Log($"[HpBarUI] 싱글 PlayerController 발견: {_singlePlayer.name}");

                // 싱글은 상대방 패널 숨기기
                var root = GetComponent<UIDocument>().rootVisualElement;
                var ep   = root.Q<VisualElement>("enemy-panel");
                if (ep != null) ep.style.display = DisplayStyle.None;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // UI 갱신
    // ─────────────────────────────────────────────────────────
    private void RefreshUI()
    {
        if (NetworkClient.active && _myNet != null)
        {
            SetFill(_myFill,    _myNet.health,   _myNet.maxHealth,   _myHpText);
            if (_enemyNet != null)
                SetFill(_enemyFill, _enemyNet.health, _enemyNet.maxHealth, _enemyHpText);
        }
        else if (_singlePlayer != null)
        {
            SetFill(_myFill, _singleHp, _singleMaxHp, _myHpText);
        }
    }

    /// <summary>
    /// 싱글 플레이에서 외부에서 HP 변경 시 호출 (데미지 시스템 추가 시 연동).
    /// </summary>
    public void SetSinglePlayerHp(float current, float max)
    {
        _singleHp    = current;
        _singleMaxHp = max;
    }

    // ─────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────
    private static void SetFill(VisualElement fill, float current, float max, Label text)
    {
        if (fill == null) return;
        float pct = max > 0f ? Mathf.Clamp01(current / max) * 100f : 0f;
        fill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
        if (text != null)
            text.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void SetMyName(string n)
    {
        if (_myName != null) _myName.text = string.IsNullOrEmpty(n) ? "Player" : n;
    }

    private void SetEnemyName(string n)
    {
        if (_enemyName != null) _enemyName.text = string.IsNullOrEmpty(n) ? "Enemy" : n;
    }

    private static string GetCharacterName(PlayerNetwork pn)
    {
        if (pn == null) return "";
        // 캐릭터 오브젝트에서 이름 추출 시도
        if (pn.currentCharacter != null)
        {
            CharaStat cs = pn.currentCharacter.GetComponent<CharaStat>();
            if (cs != null && cs.characterStats != null)
                return cs.characterStats.characterName;
            return pn.currentCharacter.name;
        }
        return $"Player {pn.netId}";
    }
}
