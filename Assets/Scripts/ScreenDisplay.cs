using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示器屏幕上的内容（世界空间 Canvas，贴在显示器上）。
/// 两个模式：摸鱼 = 一局射击游戏 / 工作 = Excel 表格。
/// </summary>
public class ScreenDisplay : MonoBehaviour
{
    Image _bg, _grid, _titleBar, _flash, _barBg, _barFill;
    Text _title, _body, _status, _barLabel;
    ShooterScreen _shooter;

    public bool Slacking { get; private set; } = true;

    int _bodyIdx = -1, _titleIdx = -1;
    float _cycleTimer, _flashTimer, _workSeconds, _slackSeconds;

    static readonly Color SlackBg = new Color(0.055f, 0.07f, 0.105f);
    static readonly Color WorkBg = new Color(0.905f, 0.915f, 0.935f);
    static readonly Color SlackBar = new Color(0.11f, 0.15f, 0.13f);
    static readonly Color WorkBar = new Color(0.07f, 0.32f, 0.25f);

    public static ScreenDisplay Create(Transform parent)
    {
        // 注意：世界空间 Canvas 不旋转，文字才是正的。
        // UI 着色器默认 Cull Off，相机从背面看照样能看见，且不会被镜像。
        var canvas = UIFactory.WorldCanvas(parent, "ScreenCanvas", new Vector2(900f, 507f),
            new Vector3(OfficeBuilder.MonitorCenter.x, OfficeBuilder.MonitorCenter.y,
                        OfficeBuilder.MonitorCenter.z - 0.015f),
            Vector3.zero, OfficeBuilder.MonitorWidth);
        var sd = canvas.gameObject.AddComponent<ScreenDisplay>();
        sd.Build(canvas.transform);
        return sd;
    }

    void Build(Transform root)
    {
        _bg = UIFactory.Img(root, "Bg", SlackBg);
        UIFactory.Stretch(_bg.rectTransform);

        _grid = UIFactory.Img(root, "SheetGrid", new Color(1f, 1f, 1f, 1f));
        UIFactory.Stretch(_grid.rectTransform);
        _grid.color = new Color(0.94f, 0.95f, 0.92f);
        BuildSpreadsheet(_grid.transform);
        _grid.type = Image.Type.Simple;

        // 摸鱼时的那局射击游戏（放在标题栏/状态栏之前，所以会被它们压在下面）
        _shooter = ShooterScreen.Create(root);

        // 正文（只有工作模式显示）
        _body = UIFactory.Label(root, "Body", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        var br = _body.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
        br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(840f, 34f);
        br.anchoredPosition = new Vector2(0f, -164f);

        // 标题栏
        _titleBar = UIFactory.Img(root, "TitleBar", SlackBar);
        var tb = _titleBar.rectTransform;
        tb.anchorMin = new Vector2(0f, 1f);
        tb.anchorMax = new Vector2(1f, 1f);
        tb.pivot = new Vector2(0.5f, 1f);
        tb.sizeDelta = new Vector2(0f, 46f);
        tb.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 3; i++)
        {
            var dot = UIFactory.Img(_titleBar.transform, "Dot" + i, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Pin(dot.rectTransform, new Vector2(0f, 1f), new Vector2(14f + i * 24f, -14f), new Vector2(13f, 13f));
        }

        _title = UIFactory.Label(_titleBar.transform, "Title", "", 26, Color.white, TextAnchor.MiddleLeft);
        var tr = _title.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(92f, 0f); tr.offsetMax = new Vector2(-16f, 0f);

        // 底部进度条（只有工作模式显示）
        _barBg = UIFactory.Img(root, "BarBg", new Color(1f, 1f, 1f, 0.20f));
        var bb = _barBg.rectTransform;
        bb.anchorMin = new Vector2(0f, 0f);
        bb.anchorMax = new Vector2(1f, 0f);
        bb.pivot = new Vector2(0.5f, 0f);
        bb.sizeDelta = new Vector2(-60f, 10f);
        bb.anchoredPosition = new Vector2(0f, 48f);

        _barFill = UIFactory.Img(_barBg.transform, "BarFill", Color.white);
        var bf = _barFill.rectTransform;
        bf.anchorMin = new Vector2(0f, 0f);
        bf.anchorMax = new Vector2(0.02f, 1f);
        bf.offsetMin = Vector2.zero; bf.offsetMax = Vector2.zero;

        _barLabel = UIFactory.Label(root, "BarLabel", "", 20, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
        var blr = _barLabel.rectTransform;
        blr.anchorMin = blr.anchorMax = new Vector2(0.5f, 0f);
        blr.pivot = new Vector2(0.5f, 0f);
        blr.sizeDelta = new Vector2(800f, 26f);
        blr.anchoredPosition = new Vector2(0f, 62f);

        // 状态栏
        var statusBar = UIFactory.Img(root, "StatusBar", new Color(0f, 0f, 0f, 0.42f));
        var sb = statusBar.rectTransform;
        sb.anchorMin = new Vector2(0f, 0f);
        sb.anchorMax = new Vector2(1f, 0f);
        sb.pivot = new Vector2(0.5f, 0f);
        sb.sizeDelta = new Vector2(0f, 30f);
        sb.anchoredPosition = Vector2.zero;

        _status = UIFactory.Label(statusBar.transform, "StatusText", "", 18,
            new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft);
        var str = _status.rectTransform;
        str.anchorMin = Vector2.zero; str.anchorMax = Vector2.one;
        str.offsetMin = new Vector2(14f, 0f); str.offsetMax = new Vector2(-14f, 0f);

        // 切换时的白闪
        _flash = UIFactory.Img(root, "Flash", new Color(1f, 1f, 1f, 0f));
        UIFactory.Stretch(_flash.rectTransform);

        ApplyMode(true, force: true);
    }

    void BuildSpreadsheet(Transform parent)
    {
        var formula = UIFactory.Label(parent, "Formula", "fx   = SUM(D4:D10)          |          自动保存已开启", 19,
            new Color(0.25f, 0.33f, 0.30f), TextAnchor.MiddleLeft);
        UIFactory.Pin(formula.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 177f), new Vector2(810f, 32f));
        string[,] data = {
            { "项目编号", "任务名称", "负责人", "完成进度", "优先级", "状态" },
            { "MY-001", "季度复盘", "小李", "86%", "P1", "进行中" },
            { "MY-002", "用户调研", "小王", "100%", "P0", "已完成" },
            { "MY-003", "预算审核", "小张", "72%", "P1", "待审核" },
            { "MY-004", "方案对齐", "小李", "95%", "P2", "进行中" },
            { "MY-005", "数据报表", "小陈", "100%", "P1", "已完成" },
            { "MY-006", "周会准备", "小李", "60%", "P2", "进行中" }
        };
        for (int row = 0; row < 7; row++) for (int col = 0; col < 6; col++)
        {
            var cell = UIFactory.Img(parent, "Cell" + row + "_" + col,
                row == 0 ? new Color(0.12f, 0.37f, 0.29f) :
                row % 2 == 0 ? new Color(0.86f, 0.91f, 0.87f) : new Color(0.98f, 0.98f, 0.95f));
            UIFactory.Pin(cell.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(-337.5f + col * 135f, 128f - row * 43f), new Vector2(133f, 41f));
            var text = UIFactory.Label(cell.transform, "Value", data[row, col], 21,
                row == 0 ? Color.white : new Color(0.16f, 0.24f, 0.22f), TextAnchor.MiddleCenter,
                row == 0 ? FontStyle.Bold : FontStyle.Normal);
            UIFactory.Stretch(text.rectTransform);
        }
    }

    public void ResetRound()
    {
        _workSeconds = _slackSeconds = 0f;
        _shooter.ResetRound();
        ApplyMode(true, force: true);
    }

    // ── 模式 ─────────────────────────────────────────────────
    public void SetSlacking(bool slack)
    {
        if (slack == Slacking) return;
        ApplyMode(slack, force: false);
    }

    public void Toggle() { ApplyMode(!Slacking, force: false); }

    void ApplyMode(bool slack, bool force)
    {
        Slacking = slack;
        _flashTimer = 0.13f;

        if (slack)
        {
            _bg.color = SlackBg;
            _grid.gameObject.SetActive(false);
            _body.gameObject.SetActive(false);
            _barBg.gameObject.SetActive(false);
            _barLabel.gameObject.SetActive(false);
            _shooter.SetActive(true);

            _titleBar.color = SlackBar;
            _status.color = new Color(0.75f, 0.9f, 0.8f, 0.9f);
            _titleIdx = force ? -1 : _titleIdx;
            NewTitle(Meme.ShooterTitles);

            if (OfficeBuilder.MonitorGlow != null)
                OfficeBuilder.MonitorGlow.color = new Color(0.5f, 0.75f, 1f);
        }
        else
        {
            _bg.color = WorkBg;
            _grid.gameObject.SetActive(true);
            _body.gameObject.SetActive(false);
            _barBg.gameObject.SetActive(true);
            _barLabel.gameObject.SetActive(true);
            _shooter.SetActive(false);

            _titleBar.color = WorkBar;
            _body.color = new Color(0.16f, 0.19f, 0.25f);
            _barLabel.color = new Color(0.15f, 0.18f, 0.24f, 0.75f);
            _status.color = new Color(0.12f, 0.15f, 0.20f, 0.8f);
            _status.text = "就绪";
            _bodyIdx = -1;
            NewTitle(Meme.WorkTitles);
            NewBody(Meme.WorkBodies);

            if (OfficeBuilder.MonitorGlow != null)
                OfficeBuilder.MonitorGlow.color = new Color(0.75f, 0.82f, 0.95f);
        }
        _cycleTimer = 3.2f;
    }

    void NewTitle(string[] arr)
    {
        int i = Random.Range(0, arr.Length);
        if (arr.Length > 1 && i == _titleIdx) i = (i + 1) % arr.Length;
        _titleIdx = i;
        _title.text = arr[i];
    }

    void NewBody(string[] arr)
    {
        int i = Random.Range(0, arr.Length);
        if (arr.Length > 1 && i == _bodyIdx) i = (i + 1) % arr.Length;
        _bodyIdx = i;
        _body.text = arr[i].Replace("\n", "  ·  ");
    }

    // ── 每帧 ─────────────────────────────────────────────────
    void Update()
    {
        float dt = Time.deltaTime;

        if (SlackGame.I != null && SlackGame.I.State != SlackGame.GameState.Playing) return;
        if (Slacking) _slackSeconds += dt; else _workSeconds += dt;

        _flashTimer = Mathf.Max(0f, _flashTimer - dt);
        _flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_flashTimer / 0.13f) * 0.85f);

        if (Slacking)
        {
            // 射击游戏有自己的 HUD，这里只维护窗口标题和底部状态栏
            _cycleTimer -= dt;
            if (_cycleTimer <= 0f)
            {
                _cycleTimer = Random.Range(6f, 11f);
                NewTitle(Meme.ShooterTitles);
            }
            _status.text = "对局 " + Fmt(_slackSeconds)
                         + "   ·   击杀 " + _shooter.Kills
                         + "   ·   比分 " + _shooter.Score
                         + "   ·   延迟 " + (3 + _shooter.Kills % 7) + "ms"
                         + "   ·   状态：" + (_shooter.Hp > 50 ? "上分中" : "残血了别打了");
            return;
        }

        // ── 工作模式：表格正文轮播 + 上传进度 ──
        _cycleTimer -= dt;
        if (_cycleTimer <= 0f)
        {
            _cycleTimer = Random.Range(4.0f, 6.5f);
            NewBody(Meme.WorkBodies);
            if (Random.value < 0.25f) NewTitle(Meme.WorkTitles);
        }

        float p = Mathf.Repeat(_workSeconds * 0.18f, 1f);
        SetBar(p);
        _barLabel.text = "正在同步工作表…  " + Mathf.RoundToInt(p * 100f) + "%   ·   自动保存已开启";
        _status.text = "本次工作 " + Fmt(_workSeconds) + "   ·   窗口焦点：Excel  ·   状态：努力";
    }

    void SetBar(float v)
    {
        var r = _barFill.rectTransform;
        r.anchorMax = new Vector2(Mathf.Clamp(v, 0.02f, 1f), 1f);
        _barFill.color = new Color(0.35f, 0.72f, 0.45f);
    }

    /// <summary>编辑器预览：摆出"正在打枪"的静态一帧，不按 Play 也能看见。</summary>
    public void EditorPreview()
    {
        ApplyMode(true, force: true);
        _shooter.EditorPreview();
        _status.text = "对局 00:47   ·   击杀 12   ·   比分 1280   ·   延迟 5ms   ·   状态：上分中";
    }

    static string Fmt(float s)
    {
        int t = Mathf.FloorToInt(s);
        return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
    }
}
