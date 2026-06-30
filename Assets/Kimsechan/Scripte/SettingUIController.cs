using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingUIController : MonoBehaviour
{
    private struct ResolutionOption
    {
        public int width;
        public int height;

        public ResolutionOption(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    [Header("오디오 믹서")]
    public AudioMixer audioMixer;
    public string masterVolumeParameter = "MasterVolume";
    public string bgmVolumeParameter = "BGMVolume";
    public string sfxVolumeParameter = "SFXVolume";

    [Header("오디오 소스 직접 연결")]
    public AudioSource[] bgmSources;
    public AudioSource[] sfxSources;

    private readonly ResolutionOption[] resolutionOptions =
    {
        new ResolutionOption(1280, 720),
        new ResolutionOption(1600, 900),
        new ResolutionOption(1920, 1080),
        new ResolutionOption(2560, 1440)
    };

    private readonly int[] fpsOptions = { 30, 60, 120, 144, -1 };

    private VisualElement root;
    private Slider masterSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Toggle windowModeToggle;
    private Label masterValueLabel;
    private Label bgmValueLabel;
    private Label sfxValueLabel;
    private Label windowModeValueLabel;
    private Label resolutionValueLabel;
    private Label fpsValueLabel;
    private int resolutionIndex = 2;
    private int fpsIndex = 1;

    private void Awake()
    {
        BindElements();
        LoadSettings();
        BindEvents();
        RefreshAllLabels();
        ApplySettings();
    }

    private void BindElements()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        masterSlider = root.Q<Slider>("master-volume-slider");
        bgmSlider = root.Q<Slider>("bgm-volume-slider");
        sfxSlider = root.Q<Slider>("sfx-volume-slider");
        windowModeToggle = root.Q<Toggle>("window-mode-toggle");

        masterValueLabel = root.Q<Label>("master-volume-value");
        bgmValueLabel = root.Q<Label>("bgm-volume-value");
        sfxValueLabel = root.Q<Label>("sfx-volume-value");
        windowModeValueLabel = root.Q<Label>("window-mode-value");
        resolutionValueLabel = root.Q<Label>("resolution-value");
        fpsValueLabel = root.Q<Label>("fps-value");
    }

    private void BindEvents()
    {
        if (masterSlider != null)
            masterSlider.RegisterValueChangedCallback(_ => OnSoundChanged());

        if (bgmSlider != null)
            bgmSlider.RegisterValueChangedCallback(_ => OnSoundChanged());

        if (sfxSlider != null)
            sfxSlider.RegisterValueChangedCallback(_ => OnSoundChanged());

        if (windowModeToggle != null)
            windowModeToggle.RegisterValueChangedCallback(_ => RefreshWindowModeLabel());

        Button resolutionPrevButton = root.Q<Button>("resolution-prev-button");
        Button resolutionNextButton = root.Q<Button>("resolution-next-button");
        Button fpsPrevButton = root.Q<Button>("fps-prev-button");
        Button fpsNextButton = root.Q<Button>("fps-next-button");
        Button applyButton = root.Q<Button>("setting-apply-button");
        Button closeButton = root.Q<Button>("setting-close-button");

        if (resolutionPrevButton != null)
            resolutionPrevButton.clicked += () => ChangeResolution(-1);

        if (resolutionNextButton != null)
            resolutionNextButton.clicked += () => ChangeResolution(1);

        if (fpsPrevButton != null)
            fpsPrevButton.clicked += () => ChangeFps(-1);

        if (fpsNextButton != null)
            fpsNextButton.clicked += () => ChangeFps(1);

        if (applyButton != null)
            applyButton.clicked += ApplySettings;

        if (closeButton != null)
            closeButton.clicked += CloseSetting;
    }

    private void LoadSettings()
    {
        SetSliderValueWithoutNotify(masterSlider, PlayerPrefs.GetFloat("Setting_MasterVolume", 80f));
        SetSliderValueWithoutNotify(bgmSlider, PlayerPrefs.GetFloat("Setting_BGMVolume", 70f));
        SetSliderValueWithoutNotify(sfxSlider, PlayerPrefs.GetFloat("Setting_SFXVolume", 80f));

        if (windowModeToggle != null)
            windowModeToggle.SetValueWithoutNotify(PlayerPrefs.GetInt("Setting_Windowed", 1) == 1);

        resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt("Setting_ResolutionIndex", FindCurrentResolutionIndex()), 0, resolutionOptions.Length - 1);
        fpsIndex = Mathf.Clamp(PlayerPrefs.GetInt("Setting_FpsIndex", 1), 0, fpsOptions.Length - 1);
    }

    private void SetSliderValueWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 100f));
    }

    private int FindCurrentResolutionIndex()
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < resolutionOptions.Length; i++)
        {
            int distance = Mathf.Abs(Screen.width - resolutionOptions[i].width)
                + Mathf.Abs(Screen.height - resolutionOptions[i].height);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void OnSoundChanged()
    {
        RefreshSoundLabels();
        ApplySoundSettings();
        SaveSoundSettings();
    }

    private void ChangeResolution(int direction)
    {
        resolutionIndex = WrapIndex(resolutionIndex + direction, resolutionOptions.Length);
        RefreshResolutionLabel();
    }

    private void ChangeFps(int direction)
    {
        fpsIndex = WrapIndex(fpsIndex + direction, fpsOptions.Length);
        RefreshFpsLabel();
    }

    private int WrapIndex(int index, int length)
    {
        if (length <= 0)
            return 0;

        if (index < 0)
            return length - 1;

        if (index >= length)
            return 0;

        return index;
    }

    private void ApplySettings()
    {
        ApplySoundSettings();
        ApplyScreenSettings();
        SaveSettings();
        RefreshAllLabels();
    }

    private void ApplySoundSettings()
    {
        float master = GetSliderValue(masterSlider, 80f);
        float bgm = GetSliderValue(bgmSlider, 70f);
        float sfx = GetSliderValue(sfxSlider, 80f);

        AudioListener.volume = Mathf.Clamp01(master / 100f);

        if (audioMixer != null)
        {
            audioMixer.SetFloat(masterVolumeParameter, PercentToDecibel(master));
            audioMixer.SetFloat(bgmVolumeParameter, PercentToDecibel(bgm));
            audioMixer.SetFloat(sfxVolumeParameter, PercentToDecibel(sfx));
        }

        ApplyAudioSourceVolume(GetBgmSources(), bgm);
        ApplyAudioSourceVolume(GetSfxSources(), sfx);
    }

    private float GetSliderValue(Slider slider, float fallback)
    {
        return slider != null ? Mathf.Clamp(slider.value, 0f, 100f) : fallback;
    }

    private float PercentToDecibel(float percent)
    {
        if (percent <= 0.01f)
            return -80f;

        return Mathf.Log10(Mathf.Clamp01(percent / 100f)) * 20f;
    }

    private void ApplyAudioSourceVolume(IEnumerable<AudioSource> sources, float percent)
    {
        float volume = Mathf.Clamp01(percent / 100f);

        foreach (AudioSource source in sources)
        {
            if (source != null)
                source.volume = volume;
        }
    }

    private IEnumerable<AudioSource> GetBgmSources()
    {
        if (bgmSources != null && bgmSources.Length > 0)
            return bgmSources;

        return FindAudioSourcesByName("bgm", "music");
    }

    private IEnumerable<AudioSource> GetSfxSources()
    {
        if (sfxSources != null && sfxSources.Length > 0)
            return sfxSources;

        return FindAudioSourcesByName("sfx", "effect", "sound");
    }

    private List<AudioSource> FindAudioSourcesByName(params string[] keywords)
    {
        List<AudioSource> matches = new List<AudioSource>();
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource source in sources)
        {
            string lowerName = source.gameObject.name.ToLower();

            foreach (string keyword in keywords)
            {
                if (lowerName.Contains(keyword))
                {
                    matches.Add(source);
                    break;
                }
            }
        }

        return matches;
    }

    private void ApplyScreenSettings()
    {
        ResolutionOption option = resolutionOptions[resolutionIndex];
        bool windowed = windowModeToggle == null || windowModeToggle.value;
        FullScreenMode mode = windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

        Screen.SetResolution(option.width, option.height, mode);
        Application.targetFrameRate = fpsOptions[fpsIndex];
        QualitySettings.vSyncCount = 0;
    }

    private void SaveSettings()
    {
        SaveSoundSettings();
        PlayerPrefs.SetInt("Setting_Windowed", windowModeToggle == null || windowModeToggle.value ? 1 : 0);
        PlayerPrefs.SetInt("Setting_ResolutionIndex", resolutionIndex);
        PlayerPrefs.SetInt("Setting_FpsIndex", fpsIndex);
        PlayerPrefs.Save();
    }

    private void SaveSoundSettings()
    {
        PlayerPrefs.SetFloat("Setting_MasterVolume", GetSliderValue(masterSlider, 80f));
        PlayerPrefs.SetFloat("Setting_BGMVolume", GetSliderValue(bgmSlider, 70f));
        PlayerPrefs.SetFloat("Setting_SFXVolume", GetSliderValue(sfxSlider, 80f));
    }

    private void RefreshAllLabels()
    {
        RefreshSoundLabels();
        RefreshWindowModeLabel();
        RefreshResolutionLabel();
        RefreshFpsLabel();
    }

    private void RefreshSoundLabels()
    {
        SetPercentLabel(masterValueLabel, masterSlider, 80f);
        SetPercentLabel(bgmValueLabel, bgmSlider, 70f);
        SetPercentLabel(sfxValueLabel, sfxSlider, 80f);
    }

    private void SetPercentLabel(Label label, Slider slider, float fallback)
    {
        if (label != null)
            label.text = $"{Mathf.RoundToInt(GetSliderValue(slider, fallback))}%";
    }

    private void RefreshWindowModeLabel()
    {
        if (windowModeValueLabel != null)
            windowModeValueLabel.text = windowModeToggle == null || windowModeToggle.value ? "창모드" : "전체화면";
    }

    private void RefreshResolutionLabel()
    {
        if (resolutionValueLabel == null)
            return;

        ResolutionOption option = resolutionOptions[resolutionIndex];
        resolutionValueLabel.text = $"{option.width} x {option.height}";
    }

    private void RefreshFpsLabel()
    {
        if (fpsValueLabel == null)
            return;

        int fps = fpsOptions[fpsIndex];
        fpsValueLabel.text = fps == -1 ? "제한 없음" : fps.ToString();
    }

    private void CloseSetting()
    {
        if (root != null)
            root.style.display = DisplayStyle.None;
    }
}
