using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 《老板来了 · 办公室摸鱼模拟器》主控。
/// 一局目标：把摸鱼值攒满 100。老板回头时你在摸鱼，就掉一条命。
/// </summary>
public class SlackGame : MonoBehaviour
{
    public static SlackGame I;

    public enum GameState { Title, Playing, Caught, Win, Lose }

    public GameState State { get; private set; } = GameState.Title;
    public float Slack { get; private set; }
    public int Lives { get; private set; } = 3;
    public const int MaxLives = 3;
    public float Difficulty => Mathf.Clamp01(Slack / 100f);
    public bool IsSlacking => _screen != null && _screen.Slacking;
    public int PraiseCount { get; private set; }
    public float SlackSeconds { get; private set; }
    public const float RoundDuration = 75f;
    public float TimeLeft { get; private set; } = RoundDuration;
    public float Flow { get; private set; }
    public float Multiplier => 1f + Mathf.Clamp01(Flow / 6f) * 0.6f;
    public bool IsPaused { get; private set; }
    bool _timedOut;

    const float SlackRate = 4.4f;
    const float WorkDecay = 1.2f;
    const float WinAt = 100f;
    /// <summary>16:9 下 vFOV=70° 对应的水平视野，作为设计基准。</summary>
    const float DesignHFov = 100f;

    ScreenDisplay _screen;
    HudUI _hud;
    BossController _boss;
    AudioSource _audio;

    AudioClip _sfxStep, _sfxHeart, _sfxClick, _sfxBlip, _sfxAlert, _sfxCaught, _sfxPraise, _sfxDoor, _sfxWin, _sfxLose, _sfxTick;

    float _heartTimer, _blipTimer, _inputLock;
    float _dangerSmooth;
    float _toggleReady;
    int _caughtCount;

    // ── 启动 ─────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<SlackGame>() != null) return;
        var go = new GameObject("SlackGame");
        go.AddComponent<SlackGame>();
    }

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        // Editor 失焦时 Unity 默认会冻结播放循环，这游戏要一直在跑
        Application.runInBackground = true;

        OfficeBuilder.BuildWorld();

        // 直接拿 BuildWorld 刚刚创建的那一份实例。
        // 千万不能用 FindAnyObjectByType：BuildWorld 会先销毁旧的 GameRoot，
        // 那种写法可能抓到"即将被销毁"的旧对象，一帧后全部变成 MissingReference。
        _screen = OfficeBuilder.Screen;
        _hud = OfficeBuilder.Hud;
        _boss = OfficeBuilder.Boss;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f;
        _audio.playOnAwake = false;
        _sfxStep = AudioSynth.Footstep();
        _sfxHeart = AudioSynth.Heartbeat();
        _sfxClick = AudioSynth.Click();
        _sfxBlip = AudioSynth.Blip();
        _sfxAlert = AudioSynth.Alert();
        _sfxCaught = AudioSynth.Caught();
        _sfxPraise = AudioSynth.Praise();
        _sfxDoor = AudioSynth.DoorCreak();
        _sfxWin = AudioSynth.Win();
        _sfxLose = AudioSynth.Lose();
        _sfxTick = AudioSynth.Tick();

        _boss.Init(this, OfficeBuilder.BossBody, OfficeBuilder.BossBubblePivot, OfficeBuilder.BossBubble,
                   OfficeBuilder.BossAlertPivot, OfficeBuilder.BossAlert, OfficeBuilder.Cam);

        EnterTitle();
    }

    // ── 状态切换 ─────────────────────────────────────────────

    void EnterTitle()
    {
        State = GameState.Title;
        _boss.Halt();
        Slack = 0f;
        Lives = MaxLives;
        PraiseCount = 0;
        SlackSeconds = 0f;
        _caughtCount = 0;
        TimeLeft = RoundDuration;
        Flow = 0f;
        _timedOut = false;
        _toggleReady = 0f;
        _dangerSmooth = 0f;
        _heartTimer = _blipTimer = 0f;
        _screen.ResetRound();

        _screen.SetSlacking(true);
        _screen.enabled = true;

        _hud.ShowOverlay(true);
        _hud.SetOverlay(
            Meme.Title,
            new Color(1f, 0.84f, 0.28f),
            Meme.Subtitle,
            string.Join("\n", Meme.TitleRules),
            Meme.StartHint,
            new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.time * 3f)));
        _hud.SetSlack(0f);
        _hud.SetLives(Lives, MaxLives);
        _hud.SetDanger(0f);
        _hud.SetHint("");
        _hud.Radar(0f, 0f, "", false, Color.green);
        _inputLock = Time.time + 0.35f;
    }

    void StartRound()
    {
        State = GameState.Playing;
        Slack = 0f;
        Lives = MaxLives;
        PraiseCount = 0;
        SlackSeconds = 0f;
        _caughtCount = 0;
        TimeLeft = RoundDuration;
        Flow = 0f;
        _timedOut = false;
        _toggleReady = 0f;
        _dangerSmooth = 0f;
        _heartTimer = _blipTimer = 0f;
        _screen.ResetRound();
        _boss.Halt();
        _screen.SetSlacking(true);
        _hud.ShowOverlay(false);
        _hud.SetSlack(0f);
        _hud.SetLives(Lives, MaxLives);
        _hud.SetDanger(0f);
        _hud.SetHint("空格 / 点击  切换窗口     ·     连续摸鱼 6 秒，收益升至 ×1.6");
        _hud.Big("开工！", new Color(1f, 0.9f, 0.4f), 1.4f);
        _boss.Begin();
        _inputLock = Time.time + 0.2f;
    }

    void Win()
    {
        if (State == GameState.Win) return;
        State = GameState.Win;
        _boss.Halt();
        _screen.SetSlacking(false);
        Play(_sfxWin, 0.9f);
        _hud.SetDanger(0f);
        _hud.Radar(0f, 0f, "", false, Color.green);

        int score = Mathf.RoundToInt(Slack) + PraiseCount * 4 - _caughtCount * 12;
        score = Mathf.Max(0, score);
        _hud.ShowOverlay(true);
        _hud.SetOverlay(
            "下 班 了",
            new Color(0.5f, 1f, 0.6f),
            "评级：" + Meme.Grade(score) + "\n" +
            "影帝切屏 ×" + PraiseCount + "   /   被抓 " + _caughtCount + " 次   /   耗时 " + (RoundDuration - TimeLeft).ToString("F1") + " 秒\n综合得分 " + score,
            string.Join("\n", Meme.WinLines),
            "按任意键再来一天",
            new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.time * 3f)));
        _inputLock = Time.time + 0.9f;
    }

    void Lose()
    {
        if (State == GameState.Lose) return;
        State = GameState.Lose;
        _boss.Halt();
        _screen.SetSlacking(false);
        Play(_sfxLose, 0.9f);
        _hud.SetDanger(0f);
        _hud.Radar(0f, 0f, "", false, Color.green);

        _hud.ShowOverlay(true);
        _hud.SetOverlay(
            _timedOut ? "下班打卡失败" : "你被优化了",
            new Color(1f, 0.35f, 0.3f),
            "本次摸鱼值：" + Mathf.RoundToInt(Slack) + " / 100\n" +
            "影帝级切屏 ×" + PraiseCount + "      被逮到 ×" + _caughtCount,
            _timedOut ? "今天太认真工作，摸鱼指标没有完成。\n再来一次，试着抓住老板接电话的机会。" : string.Join("\n", Meme.LoseLines),
            "按任意键重新投简历",
            new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.time * 3f)));
        _inputLock = Time.time + 0.9f;
    }

    // ── 老板回调 ─────────────────────────────────────────────

    public void OnFakeAlarm()
    {
        if (State != GameState.Playing) return;
        Play(_sfxDoor, 0.5f);
        _hud.Flash(new Color(1f, 0.25f, 0.2f), 0.35f);
        _hud.Big(Meme.Pick(Meme.FakeAlarm), new Color(1f, 0.5f, 0.4f), 1.1f);
        _hud.SetDanger(0.35f);
    }

    public void OnBossEnter()
    {
        if (State != GameState.Playing) return;
        Play(_sfxTick, 0.35f);
        _hud.SetHint("老板出来了 —— 注意雷达");
    }

    public void OnBossPhone()
    {
        if (State != GameState.Playing) return;
        Play(_sfxTick, 0.4f);
        _hud.Big("老板在打电话 —— 放心摸！", new Color(0.45f, 1f, 0.65f), 1.5f);
        _hud.SetHint("安全窗口：老板在接电话，抓紧摸鱼");
    }

    public void OnBossWatchStart()
    {
        if (State != GameState.Playing) return;
        Play(_sfxAlert, 0.45f);
    }

    public void OnBossWatchEnd()
    {
        if (State != GameState.Playing) return;
        _hud.SetHint("老板转身了…… 继续");
    }

    public void OnCaught()
    {
        if (State != GameState.Playing) return;
        State = GameState.Caught;
        Flow = 0f;
        _caughtCount++;
        Lives = Mathf.Max(0, Lives - 1);
        Slack = Mathf.Max(0f, Slack - Mathf.Max(15f, Slack * 0.5f)); // 被抓失去一半进度，至少 15 点
        _screen.SetSlacking(false);       // 手忙脚乱切回工作
        Play(_sfxCaught, 0.85f);
        _hud.Flash(new Color(1f, 0.15f, 0.12f), 0.6f);
        _hud.SetLives(Lives, MaxLives);
        _hud.Big("被 发 现 了", new Color(1f, 0.25f, 0.2f), 1.6f);
        _hud.SetHint("……");
    }

    public void OnLecture(string line)
    {
        _hud.Big("老板：" + line, new Color(1f, 0.85f, 0.55f), 2.4f);
    }

    public void OnBossLeave()
    {
        if (State == GameState.Win || State == GameState.Lose) return;
        if (Lives <= 0) { Lose(); return; }

        State = GameState.Playing;
        _screen.SetSlacking(true);
        _hud.SetHint("老板走了 —— 继续摸");
        _hud.Big("老板走了，继续", new Color(0.6f, 1f, 0.7f), 1.4f);
        Play(_sfxTick, 0.4f);
    }

    public void AwardPraise()
    {
        if (State != GameState.Playing) return;
        PraiseCount++;
        Slack = Mathf.Min(WinAt, Slack + 2.5f);
        Play(_sfxPraise, 0.7f);
        _hud.Big(Meme.Pick(Meme.Praise), new Color(0.55f, 1f, 0.7f), 1.3f);
        _hud.Flash(new Color(0.5f, 1f, 0.7f), 0.25f);
        _hud.SetHint("影帝级切屏 ×" + PraiseCount);
    }

    public void PlayFootstep(float vol)
    {
        if (_audio == null) return;
        _audio.PlayOneShot(_sfxStep, Mathf.Clamp01(vol));
    }

    void Play(AudioClip clip, float vol)
    {
        if (_audio != null && clip != null) _audio.PlayOneShot(clip, vol);
    }

    public void SetPaused(bool paused)
    {
        if (State != GameState.Playing && State != GameState.Caught) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        _hud.ShowOverlay(paused);
        if (paused) _hud.SetOverlay("茶水间时间", new Color(1f, 0.84f, 0.53f),
            "已暂停  /  老板也在休息", "喝口水。下一次切屏，依然可以很从容。",
            "ESC  继续游戏", Color.white);
        _inputLock = Time.time + 0.15f;
    }

    static bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
        return false;
    }

    void OnDestroy()
    {
        if (I != this) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        I = null;
    }

    // ── 输入 ─────────────────────────────────────────────────
    static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) return true;
#endif
        return false;
    }

    static bool AnyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.anyKey.wasPressedThisFrame) return true;
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.anyKeyDown) return true;
#endif
        return false;
    }

    // ── 每帧 ─────────────────────────────────────────────────
    void Update()
    {
        if (PausePressed() && (State == GameState.Playing || State == GameState.Caught))
            SetPaused(!IsPaused);
        UpdateCameraFov();
        if (IsPaused) return;
        float dt = Time.deltaTime;
        if (State == GameState.Playing || State == GameState.Caught)
        {
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
            if (TimeLeft <= 0f) { _timedOut = true; Lose(); }
        }
        bool locked = Time.time < _inputLock;

        switch (State)
        {
            case GameState.Title:
                if (!locked && AnyPressed()) StartRound();
                break;

            case GameState.Playing:
                if (!locked && TogglePressed()) DoToggle();
                TickSlack(dt);
                break;

            case GameState.Caught:
                // 等老板训完话（OnBossLeave 会把状态推回去）
                break;

            case GameState.Win:
            case GameState.Lose:
                if (!locked && AnyPressed()) StartRound();
                break;
        }

        UpdateHud(dt);
        UpdateSfx(dt);
    }

    /// <summary>
    /// 相机竖 FOV 跟着画面比例走：无论 Game View 是 16:9 还是被拉成方的，
    /// 横向视野都尽量保持设计值，老板不会被裁到画面外。
    /// </summary>
    void UpdateCameraFov()
    {
        var cam = OfficeBuilder.Cam;
        if (cam == null) return;
        float vFromH = 2f * Mathf.Atan(Mathf.Tan(DesignHFov * 0.5f * Mathf.Deg2Rad) /
                                        Mathf.Max(0.25f, cam.aspect)) * Mathf.Rad2Deg;
        float fov = Mathf.Clamp(vFromH, 62f, 82f);
        if (Mathf.Abs(cam.fieldOfView - fov) > 0.01f) cam.fieldOfView = fov;
    }

    void DoToggle()
    {
        if (Time.unscaledTime < _toggleReady) return;
        _toggleReady = Time.unscaledTime + 0.16f;
        _screen.Toggle();
        Flow = 0f;
        Play(_sfxClick, _screen.Slacking ? 0.5f : 0.75f);
    }

    void TickSlack(float dt)
    {
        if (Slack >= WinAt) { Win(); return; }
        if (_screen.Slacking)
        {
            Flow += dt;
            Slack = Mathf.Min(WinAt, Slack + SlackRate * Multiplier * dt);
            SlackSeconds += dt;
            _blipTimer -= dt;
            if (_blipTimer <= 0f)
            {
                _blipTimer = 0.85f;
                Play(_sfxBlip, 0.16f);
            }
            if (Slack >= WinAt) { Win(); return; }
        }
        else
        {
            Slack = Mathf.Max(0f, Slack - WorkDecay * dt);
        }
    }

    void UpdateHud(float dt)
    {
        if (State == GameState.Title || State == GameState.Win || State == GameState.Lose) return;

        _hud.SetSlack(Slack / WinAt);
        _hud.SetLives(Lives, MaxLives);
        _hud.SetPraise(PraiseCount);
        _hud.SetSession(TimeLeft, _screen.Slacking, Multiplier, Flow / 6f,
            _boss.Exposure, _boss.State == BossController.BState.Watch);

        bool vis = _boss.Visible;
        Color c = Color.Lerp(new Color(0.45f, 1f, 0.55f), new Color(1f, 0.22f, 0.18f), _boss.Danger);
        if (_boss.State == BossController.BState.Watch) c = new Color(1f, 0.22f, 0.18f);
        else if (_boss.State == BossController.BState.Warn) c = new Color(1f, 0.78f, 0.2f);
        _hud.Radar(_boss.X, _boss.Danger, _boss.StateLabel(), vis, c);

        float want = _boss.Danger;
        if (_boss.State == BossController.BState.Watch) want = 1f;
        _dangerSmooth = Mathf.Lerp(_dangerSmooth, want, 1f - Mathf.Exp(-dt * 7f));
        _hud.SetDanger(_dangerSmooth);
    }

    void UpdateSfx(float dt)
    {
        if (State != GameState.Playing) return;
        bool tense = _boss.State == BossController.BState.Watch || _boss.State == BossController.BState.Warn;
        if (tense)
        {
            _heartTimer -= dt;
            if (_heartTimer <= 0f)
            {
                _heartTimer = _boss.State == BossController.BState.Watch ? 0.62f : 0.95f;
                Play(_sfxHeart, _boss.State == BossController.BState.Watch ? 0.5f : 0.3f);
            }
        }
        else _heartTimer = 0f;
    }
}
