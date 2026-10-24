using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕空间 HUD：摸鱼值、剩余机会、老板雷达、大字演出、危险红光、开始/结算面板。
/// </summary>
public class HudUI : MonoBehaviour
{
    Image _vignette, _flash, _slackFill, _radarFill, _radarBg, _radarMarker;
    Text _slackLabel, _livesLabel, _radarLabel, _hint, _big, _praiseLabel;
    RectTransform _radarTrack, _radarMarkerRt;
    Canvas _canvas;
    Text _clockLabel, _flowLabel, _modeLabel, _exposureLabel;
    Image _flowFill, _exposureFill;
    GameObject _exposurePanel;
    readonly Color _paper = new Color(0.94f, 0.92f, 0.83f);
    readonly Color _mint = new Color(0.46f, 0.88f, 0.74f);

    float _bigTimer, _bigDur, _flashTimer, _flashDur;
    Color _flashColor;
    float _danger;

    // 开始 / 结算面板
    GameObject _overlay, _playLayer;
    Text _ovTitle, _ovSub, _ovBody, _ovGrade;

    const float RadarHalfWidth = 225f;
    const float RadarWorldHalf = 4.0f;

    public static HudUI Create(Transform parent)
    {
        var canvas = UIFactory.ScreenCanvas(parent, "HudCanvas", 10, OfficeBuilder.Cam);
        var hud = canvas.gameObject.AddComponent<HudUI>();
        hud.Build(canvas);
        return hud;
    }

    void Build(Canvas canvas)
    {
        _canvas = canvas;
        _playLayer = UIFactory.Node(canvas.transform, "PlayLayer").gameObject;
        UIFactory.Stretch((RectTransform)_playLayer.transform);
        var root = _playLayer.transform;

        _vignette = UIFactory.Img(root, "Vignette", new Color(1f, 0.1f, 0.08f, 0f));
        UIFactory.Stretch(_vignette.rectTransform);
        _vignette.sprite = UIFactory.Vignette(192, new Color(1f, 0.12f, 0.10f, 1f));
        _vignette.type = Image.Type.Simple;

        var plateColor = new Color(0.025f, 0.065f, 0.075f, 0.94f);
        var leftPlate = UIFactory.Panel(root, "ProgressPlate", plateColor);
        UIFactory.Pin(leftPlate.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(330f, 100f));
        var radarPlate = UIFactory.Panel(root, "RadarPlate", plateColor);
        UIFactory.Pin(radarPlate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 100f));
        var rightPlate = UIFactory.Panel(root, "ShiftPlate", plateColor);
        UIFactory.Pin(rightPlate.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(300f, 100f));

        // ── 左上：摸鱼值 ───────────────────────────────────
        var slackPanel = UIFactory.Node(root, "SlackPanel");
        UIFactory.Pin(slackPanel, new Vector2(0f, 1f), new Vector2(44f, -38f), new Vector2(360f, 76f));

        _slackLabel = UIFactory.Label(slackPanel, "SlackLabel", "今日摸鱼  0 / 100", 24, new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Pin(_slackLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(360f, 32f));

        var slackBg = UIFactory.Img(slackPanel, "SlackBg", new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Pin(slackBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -42f), new Vector2(290f, 8f));
        _slackFill = UIFactory.Img(slackBg.transform, "Fill", new Color(1f, 0.72f, 0.18f));
        var sf = _slackFill.rectTransform;
        sf.anchorMin = new Vector2(0f, 0f); sf.anchorMax = new Vector2(0f, 1f);
        sf.offsetMin = Vector2.zero; sf.offsetMax = Vector2.zero;
        sf.sizeDelta = new Vector2(0f, 0f);

        // ── 右上：机会 ─────────────────────────────────────
        _livesLabel = UIFactory.Label(root, "Lives", "", 23, new Color(1f, 0.45f, 0.42f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_livesLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, -72f), new Vector2(260f, 30f));

        // ── 顶部中央：老板雷达 ─────────────────────────────
        _radarLabel = UIFactory.Label(root, "RadarLabel", "老板动向", 21, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Pin(_radarLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(460f, 28f));

        _radarTrack = UIFactory.Node(root, "RadarTrack");
        UIFactory.Pin(_radarTrack, new Vector2(0.5f, 1f), new Vector2(0f, -83f), new Vector2(RadarHalfWidth * 2f, 8f));

        _radarBg = UIFactory.Img(_radarTrack, "RadarBg", new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Stretch(_radarBg.rectTransform);

        // 视锥区（中间是显示器挡住的位置）
        var blind = UIFactory.Img(_radarTrack, "Blind", new Color(0.5f, 0.5f, 0.6f, 0.22f));
        float blindHalf = RadarHalfWidth * (1.6f / RadarWorldHalf);
        UIFactory.Pin(blind.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(blindHalf * 2f, 8f));

        _radarFill = UIFactory.Img(_radarTrack, "Danger", new Color(1f, 0.2f, 0.15f, 0.75f));
        var rf = _radarFill.rectTransform;
        rf.anchorMin = new Vector2(0.5f, 0f); rf.anchorMax = new Vector2(0.5f, 1f);
        rf.offsetMin = Vector2.zero; rf.offsetMax = Vector2.zero;

        _radarMarker = UIFactory.Img(_radarTrack, "Marker", new Color(0.4f, 1f, 0.5f));
        _radarMarkerRt = _radarMarker.rectTransform;
        UIFactory.Pin(_radarMarkerRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 18f));

        _clockLabel = UIFactory.Label(root, "ShiftClock", "剩余  01:15", 26, _paper, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_clockLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, -36f), new Vector2(260f, 32f));
        var bottomPlate = UIFactory.Panel(root, "ControlsPlate", plateColor);
        UIFactory.Pin(bottomPlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(700f, 70f));
        var flowPlate = UIFactory.Panel(root, "FlowPlate", plateColor);
        UIFactory.Pin(flowPlate.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(300f, 94f));
        _modeLabel = UIFactory.Label(flowPlate.transform, "Mode", "摸鱼中", 20, _mint, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Pin(_modeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(260f, 26f));
        _flowLabel = UIFactory.Label(flowPlate.transform, "FlowLabel", "连续摸鱼  ×1.0", 18, _paper, TextAnchor.MiddleLeft);
        UIFactory.Pin(_flowLabel.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -40f), new Vector2(260f, 24f));
        var flowTrack = UIFactory.Img(flowPlate.transform, "FlowTrack", new Color(1f, 1f, 1f, 0.12f));
        UIFactory.Pin(flowTrack.rectTransform, new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(264f, 4f));
        _flowFill = UIFactory.Img(flowTrack.transform, "Fill", _mint);
        UIFactory.Stretch(_flowFill.rectTransform);
        _exposurePanel = UIFactory.Panel(root, "ExposurePanel", plateColor).gameObject;
        UIFactory.Pin((RectTransform)_exposurePanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 114f), new Vector2(430f, 62f));
        _exposureLabel = UIFactory.Label(_exposurePanel.transform, "ExposureLabel", "正在被观察 · 切回工作", 20, new Color(1f, 0.6f, 0.42f));
        UIFactory.Pin(_exposureLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(400f, 26f));
        var exposureTrack = UIFactory.Img(_exposurePanel.transform, "Track", new Color(1f, 1f, 1f, 0.12f));
        UIFactory.Pin(exposureTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(390f, 6f));
        _exposureFill = UIFactory.Img(exposureTrack.transform, "Fill", new Color(1f, 0.35f, 0.23f));
        UIFactory.Stretch(_exposureFill.rectTransform);
        _exposurePanel.SetActive(false);

        // ── 底部提示 ───────────────────────────────────────
        _hint = UIFactory.Label(root, "Hint", "", 20, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter);
        UIFactory.Pin(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(660f, 32f));

        _hint.resizeTextForBestFit = true;
        _hint.resizeTextMinSize = 15; _hint.resizeTextMaxSize = 20;

        // ── 右上角：影帝连击 ───────────────────────────────
        _praiseLabel = UIFactory.Label(root, "Praise", "", 18, new Color(0.5f, 1f, 0.7f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_praiseLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 34f), new Vector2(260f, 32f));

        // ── 中央大字 ───────────────────────────────────────
        _big = UIFactory.Label(root, "Big", "", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Pin(_big.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1050f, 90f));
        _big.resizeTextForBestFit = true;
        _big.resizeTextMinSize = 23;
        _big.resizeTextMaxSize = 40;
        _big.gameObject.SetActive(false);

        // ── 全屏闪光 ───────────────────────────────────────
        _flash = UIFactory.Img(root, "Flash", new Color(1f, 1f, 1f, 0f));
        UIFactory.Stretch(_flash.rectTransform);

        BuildOverlay(canvas.transform);
    }

    void BuildOverlay(Transform root)
    {
        _overlay = UIFactory.Node(root, "Overlay").gameObject;
        UIFactory.Stretch((RectTransform)_overlay.transform);

        var dim = UIFactory.Img(_overlay.transform, "Dim", new Color(0.01f, 0.035f, 0.04f, 0.58f));
        UIFactory.Stretch(dim.rectTransform);

        var card = UIFactory.Panel(_overlay.transform, "BriefingCard", new Color(0.025f, 0.065f, 0.075f, 0.97f));
        UIFactory.Pin(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 650f));
        var rule = UIFactory.Img(card.transform, "Accent", new Color(0.88f, 0.66f, 0.36f));
        UIFactory.Pin(rule.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -84f), new Vector2(60f, 4f));
        var kicker = UIFactory.Label(card.transform, "Kicker", "M O Y U   /   办 公 室 生 存 计 划", 17, _mint, TextAnchor.MiddleLeft);
        UIFactory.Pin(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -40f), new Vector2(900f, 28f));
        _ovTitle = UIFactory.Label(_overlay.transform, "OvTitle", "", 76, new Color(1f, 0.86f, 0.35f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Pin(_ovTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(1000f, 100f));
        _ovTitle.resizeTextForBestFit = true;
        _ovTitle.resizeTextMinSize = 42;
        _ovTitle.resizeTextMaxSize = 76;

        _ovSub = UIFactory.Label(_overlay.transform, "OvSub", "", 25, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleCenter);
        UIFactory.Pin(_ovSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1000f, 86f));
        _ovSub.alignment = TextAnchor.MiddleLeft;
        _ovSub.lineSpacing = 1.35f;

        _ovBody = UIFactory.Label(_overlay.transform, "OvBody", "", 24, new Color(0.92f, 0.93f, 0.96f), TextAnchor.UpperCenter);
        UIFactory.Pin(_ovBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(1000f, 185f));
        _ovBody.alignment = TextAnchor.UpperLeft;
        _ovBody.lineSpacing = 1.5f;

        _ovGrade = UIFactory.Label(_overlay.transform, "OvGrade", "", 27, new Color(1f, 0.55f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Pin(_ovGrade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(1000f, 56f));
        _ovGrade.resizeTextForBestFit = true;
        _ovGrade.alignment = TextAnchor.MiddleLeft;
        _ovGrade.resizeTextMinSize = 20;
        _ovGrade.resizeTextMaxSize = 27;
    }

    // ── 对外 API ─────────────────────────────────────────────

    public void SetSession(float timeLeft, bool slacking, float multiplier, float flow, float exposure, bool watching)
    {
        int seconds = Mathf.CeilToInt(timeLeft);
        _clockLabel.text = "剩余  " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        _clockLabel.color = timeLeft < 15f ? new Color(1f, 0.4f, 0.3f) : _paper;
        _modeLabel.text = slacking ? "●  带薪摸鱼中" : "●  认真工作中";
        _modeLabel.color = slacking ? _mint : new Color(0.6f, 0.76f, 0.95f);
        _flowLabel.text = slacking ? "收益 ×" + multiplier.ToString("F1") + (flow >= 0.83f ? "  /  老板开始警觉" : "  /  连摸可加成") : "安全掩护  /  摸鱼值缓慢下降";
        _flowFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(flow), 1f);
        _exposurePanel.SetActive(watching);
        _exposureFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(exposure), 1f);
        _exposureLabel.text = slacking ? "视线暴露！立即切回工作" : "伪装成功  /  等老板转身";
        _exposureLabel.color = slacking ? new Color(1f, 0.55f, 0.38f) : _mint;
    }

    public void SetSlack(float v01)
    {
        float v = Mathf.Clamp01(v01);
        _slackFill.rectTransform.anchorMax = new Vector2(Mathf.Max(v, 0.001f), 1f);
        _slackLabel.text = "今日摸鱼  " + Mathf.RoundToInt(v * 100f) + " / 100";
        var c = Color.Lerp(new Color(1f, 0.72f, 0.18f), new Color(1f, 0.28f, 0.22f), v);
        _slackFill.color = c;
        _slackLabel.color = Color.Lerp(new Color(1f, 0.88f, 0.45f), new Color(1f, 0.55f, 0.4f), v);
    }

    public void SetLives(int n, int max)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < max; i++) sb.Append(i < n ? "●" : "○");
        _livesLabel.text = "容错机会   " + sb;
    }

    public void SetPraise(int n)
    {
        _praiseLabel.text = n > 0 ? "影帝级切屏  ×" + n : "";
    }

    /// <summary>xWorld: 老板的世界 x；danger: 0~1；label: 状态文字；visible: 老板是否在场</summary>
    public void Radar(float xWorld, float danger, string label, bool visible, Color color)
    {
        _radarLabel.text = visible ? "老板动向 / " + label : "老板不在  /  放心摸鱼";
        _radarLabel.color = Color.Lerp(new Color(0.75f, 0.85f, 1f), color, danger);

        _radarMarker.gameObject.SetActive(visible);
        _radarMarker.color = color;

        float x = Mathf.Clamp(xWorld / RadarWorldHalf, -1f, 1f) * RadarHalfWidth;
        _radarMarkerRt.anchoredPosition = new Vector2(x, 0f);
        _radarMarkerRt.sizeDelta = new Vector2(Mathf.Lerp(8f, 12f, danger), Mathf.Lerp(18f, 24f, danger));

        // 危险时从老板位置向中间扩散的红光
        var rf = _radarFill.rectTransform;
        float half = RadarHalfWidth * Mathf.Clamp01(Mathf.Abs(xWorld) / RadarWorldHalf) * danger;
        rf.anchorMin = new Vector2(0.5f, 0f);
        rf.anchorMax = new Vector2(0.5f, 1f);
        rf.sizeDelta = new Vector2(half * 2f, 0f);
        rf.anchoredPosition = new Vector2(x * 0.5f, 0f);
        _radarFill.color = new Color(1f, 0.2f, 0.15f, 0.25f * danger);
    }

    public void Big(string text, Color color, float duration)
    {
        _big.text = text;
        _big.color = color;
        _bigTimer = duration;
        _bigDur = duration;
        _big.gameObject.SetActive(true);
    }

    public void Flash(Color color, float duration)
    {
        _flashColor = color;
        _flashTimer = duration;
        _flashDur = duration;
    }

    public void SetDanger(float v01) { _danger = Mathf.Clamp01(v01); }

    public void SetHint(string s) { _hint.text = s; }

    public void ShowOverlay(bool on)
    {
        _overlay.SetActive(on);
        _playLayer.SetActive(!on);
        // 结算面板出现时把中央飘字收掉，免得两段文字叠在一起
        if (on) { _bigTimer = 0f; _big.gameObject.SetActive(false); }
    }

    public void SetOverlay(string title, Color titleColor, string sub, string body, string grade, Color gradeColor)
    {
        _ovTitle.text = title;
        _ovTitle.color = titleColor;
        _ovSub.text = sub;
        _ovBody.text = body;
        _ovGrade.text = grade;
        _ovGrade.color = gradeColor;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (_bigTimer > 0f)
        {
            _bigTimer -= dt;
            float t = 1f - _bigTimer / Mathf.Max(0.0001f, _bigDur);
            float a = Mathf.Clamp01(Mathf.Min(t * 6f, (1f - t) * 3.5f));
            var c = _big.color; c.a = a; _big.color = c;
            float pop = 1f + 0.12f * Mathf.Exp(-t * 8f);
            _big.rectTransform.localScale = Vector3.one * pop;
            if (_bigTimer <= 0f) _big.gameObject.SetActive(false);
        }

        if (_flashTimer > 0f)
        {
            _flashTimer -= dt;
            float a = Mathf.Clamp01(_flashTimer / Mathf.Max(0.0001f, _flashDur));
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, a * 0.75f);
        }
        else _flash.color = new Color(0f, 0f, 0f, 0f);

        // 危险红光呼吸
        float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 9f);
        var vc = _vignette.color;
        vc.a = Mathf.Lerp(vc.a, _danger * 0.85f * pulse, 1f - Mathf.Exp(-dt * 9f));
        _vignette.color = vc;
    }
}
