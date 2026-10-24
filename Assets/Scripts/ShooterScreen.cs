using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 摸鱼窗口的内容：一个自动开火的射击游戏画面。
/// 敌人从掩体后面冒头 → 准心压过去 → 开火 → 枪口火光 + 弹道 + 命中标记 + 击杀播报。
/// 纯 uGUI 拼的，不需要任何美术资源。
/// </summary>
public class ShooterScreen : MonoBehaviour
{
    // 画布 900x507，原点在中心
    const float GroundY = -70f;
    const float RiseFrom = -94f;
    const int Slots = 4;
    const int MagSize = 30;

    class Enemy
    {
        public RectTransform rt;
        public Image body, head;
        public Image flash;        // 敌人开火时的火光
        public int state;          // 0=躲着 1=冒头 2=站着 3=倒下
        public float t;
        public float x;
    }

    class Popup
    {
        public Text txt;
        public RectTransform rt;
        public float t, dur;
        public Vector2 from;
    }

    // 静态层
    Image _bg, _floor, _horizon, _redFlash, _tracer, _muzzle;
    RectTransform _scene;          // 会随开火抖动的容器
    RectTransform _crossRt, _weaponRt, _tracerRt, _muzzleRt, _hitRt;
    Image[] _crossTicks;
    Image _hitMarker;
    RectTransform _barrel;

    // HUD
    Text _ammoText, _killText, _scoreText, _hpText, _reloadText;
    Image _hpFill, _hpBg;
    Text[] _feed;
    float[] _feedLife;
    string[] _feedLine;

    Enemy[] _enemies;
    Popup[] _pops;

    // 运行状态
    bool _active;
    float _shake, _recoilX, _recoilY, _aimX, _aimY;
    float _muzzleT, _tracerT, _hitT, _redT, _reloadT, _fireCd;
    int _ammo = MagSize, _kills, _score, _hp = 100;
    float _hpRegen, _hurtCd, _popupCd, _battleTime;
    Camera _cam;

    static readonly Color GunMetal = new Color(0.30f, 0.32f, 0.36f);
    static readonly Color GunDark = new Color(0.20f, 0.22f, 0.25f);
    static readonly Color EnemyBody = new Color(0.78f, 0.26f, 0.20f);
    static readonly Color EnemyHead = new Color(0.94f, 0.55f, 0.42f);
    static readonly Color GunLight = new Color(0.46f, 0.49f, 0.55f);
    static readonly Color Crate = new Color(0.20f, 0.24f, 0.28f);
    static readonly Color CrateTop = new Color(0.27f, 0.32f, 0.37f);

    public static ShooterScreen Create(Transform parent)
    {
        var go = UIFactory.Node(parent, "ShooterScreen");
        UIFactory.Stretch(go);
        go.gameObject.AddComponent<RectMask2D>();
        var s = go.gameObject.AddComponent<ShooterScreen>();
        s.Build(go);
        return s;
    }

    // ── 构建 ─────────────────────────────────────────────────
    void Build(RectTransform root)
    {
        _cam = OfficeBuilder.Cam;

        _bg = UIFactory.Img(root, "Sky", new Color(0.055f, 0.07f, 0.105f));
        UIFactory.Stretch(_bg.rectTransform);

        _horizon = UIFactory.Img(root, "HorizonGlow", new Color(0.16f, 0.22f, 0.32f, 0.85f));
        UIFactory.Pin(_horizon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(900f, 130f));

        _floor = UIFactory.Img(root, "Floor", new Color(0.10f, 0.115f, 0.14f));
        UIFactory.Pin(_floor.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, GroundY - 84f), new Vector2(900f, 168f));

        // Perspective floor markings add depth to the training arena.
        for (int i = -4; i <= 4; i++)
        {
            var stripe = UIFactory.Img(root, "PerspectiveLine", new Color(0.32f, 0.50f, 0.53f, 0.22f));
            var rt = stripe.rectTransform;
            Vector2 from = new Vector2(i * 48f, GroundY), to = new Vector2(i * 210f, -250f);
            Vector2 delta = to - from;
            UIFactory.Pin(rt, Vector2.one * 0.5f, (from + to) / 2f, new Vector2(delta.magnitude, 2f));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        for (int i = 1; i <= 4; i++)
        {
            var line = UIFactory.Img(root, "RangeLine", new Color(0.32f, 0.50f, 0.53f, 0.18f));
            UIFactory.Pin(line.rectTransform, Vector2.one * 0.5f, new Vector2(0f, GroundY - i * i * 11f), new Vector2(900f, 2f));
        }

        // 会抖动的场景层
        _scene = UIFactory.Node(root, "Scene");

        // 远景楼房剪影
        var rnd = new System.Random(20240920);
        float bx = -460f;
        while (bx < 460f)
        {
            float w = 40f + (float)rnd.NextDouble() * 70f;
            float h = 40f + (float)rnd.NextDouble() * 110f;
            var b = UIFactory.Img(_scene, "Building", new Color(0.085f, 0.105f, 0.145f));
            UIFactory.Pin(b.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(bx + w * 0.5f, GroundY + h * 0.5f), new Vector2(w, h));
            bx += w + 8f + (float)rnd.NextDouble() * 30f;
        }

        // 敌人（在掩体之前生成，掩体后画 = 挡住腿）
        _enemies = new Enemy[Slots];
        for (int i = 0; i < Slots; i++) _enemies[i] = BuildEnemy(_scene, i);

        // 掩体
        float[] coverX = { -330f, -170f, 0f, 175f, 340f };
        float[] coverW = { 130f, 110f, 150f, 120f, 140f };
        float[] coverH = { 88f, 70f, 96f, 74f, 90f };
        for (int i = 0; i < coverX.Length; i++)
        {
            var top = UIFactory.Img(_scene, "CrateTop" + i, CrateTop);
            UIFactory.Pin(top.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(coverX[i], GroundY - coverH[i] * 0.5f + 4f), new Vector2(coverW[i], coverH[i]));
            var lip = UIFactory.Img(top.transform, "Lip", Crate);
            UIFactory.Pin(lip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(coverW[i], 12f));
        }

        // 武器（右下角，斜着）
        BuildWeapon(_scene);

        // 准心
        _crossRt = UIFactory.Node(root, "Crosshair");
        UIFactory.Pin(_crossRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(120f, 120f));
        _crossTicks = new Image[4];
        Vector2[] offs = { new Vector2(0f, 26f), new Vector2(0f, -26f), new Vector2(-26f, 0f), new Vector2(26f, 0f) };
        Vector2[] sizes = { new Vector2(4f, 19f), new Vector2(4f, 19f), new Vector2(19f, 4f), new Vector2(19f, 4f) };
        for (int i = 0; i < 4; i++)
        {
            _crossTicks[i] = UIFactory.Img(_crossRt, "Tick" + i, new Color(0.55f, 1f, 0.6f, 0.95f));
            UIFactory.Pin(_crossTicks[i].rectTransform, new Vector2(0.5f, 0.5f), offs[i], sizes[i]);
        }
        var dot = UIFactory.Img(_crossRt, "Dot", new Color(0.55f, 1f, 0.6f, 0.9f));
        UIFactory.Pin(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));

        // 弹道
        _tracer = UIFactory.Img(_scene, "Tracer", new Color(1f, 0.95f, 0.6f, 0f));
        _tracerRt = _tracer.rectTransform;
        _tracerRt.anchorMin = _tracerRt.anchorMax = new Vector2(0.5f, 0.5f);
        _tracerRt.pivot = new Vector2(0.5f, 0.5f);

        // 命中标记
        _hitMarker = UIFactory.Img(root, "HitMarker", new Color(1f, 0.35f, 0.3f, 0f));
        _hitRt = _hitMarker.rectTransform;
        _hitRt.anchorMin = _hitRt.anchorMax = new Vector2(0.5f, 0.5f);
        _hitRt.pivot = new Vector2(0.5f, 0.5f);
        _hitRt.sizeDelta = new Vector2(26f, 26f);
        var hx = UIFactory.Img(_hitRt, "H1", new Color(1f, 1f, 1f, 0.9f));
        UIFactory.Pin(hx.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 34f));
        hx.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        var h2 = UIFactory.Img(_hitRt, "H2", new Color(1f, 1f, 1f, 0.9f));
        UIFactory.Pin(h2.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 34f));
        h2.rectTransform.localRotation = Quaternion.Euler(0, 0, -45f);

        // 受伤红闪
        _redFlash = UIFactory.Img(root, "RedFlash", new Color(1f, 0.1f, 0.08f, 0f));
        UIFactory.Stretch(_redFlash.rectTransform);

        BuildHud(root);

        SetActive(false);
    }

    Enemy BuildEnemy(Transform parent, int i)
    {
        var e = new Enemy();
        var rt = UIFactory.Node(parent, "Enemy" + i);
        UIFactory.Pin(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, RiseFrom), new Vector2(70f, 110f));
        e.rt = rt;
        rt.gameObject.AddComponent<CanvasGroup>();

        e.body = UIFactory.Img(rt, "Body", EnemyBody);
        UIFactory.Pin(e.body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(54f, 74f));

        e.head = UIFactory.Panel(rt, "Head", EnemyHead);
        UIFactory.Pin(e.head.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(33f, 33f));

        var helmet = UIFactory.Img(e.head.transform, "Helmet", new Color(0.13f, 0.20f, 0.24f));
        UIFactory.Pin(helmet.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(38f, 14f));
        var vest = UIFactory.Img(e.body.transform, "Vest", new Color(0.30f, 0.19f, 0.16f));
        UIFactory.Pin(vest.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 52f));
        var belt = UIFactory.Img(vest.transform, "Belt", new Color(0.75f, 0.58f, 0.32f));
        UIFactory.Pin(belt.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 5f));
        e.flash = UIFactory.Img(rt, "EnemyFlash", new Color(1f, 0.85f, 0.4f, 0f));
        UIFactory.Pin(e.flash.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(40f, 40f));
        return e;
    }

    // 武器摆放（枪口位置跟着这几个数一起算）
    const float GunX = 245f, GunY = -125f, GunDeg = 14f;
    const float TipX = -255f, TipY = 14f;

    void BuildWeapon(Transform parent)
    {
        _weaponRt = UIFactory.Node(parent, "Weapon");
        UIFactory.Pin(_weaponRt, new Vector2(0.5f, 0.5f), new Vector2(GunX, GunY), new Vector2(340f, 220f));
        _weaponRt.localRotation = Quaternion.Euler(0f, 0f, GunDeg);

        var body = UIFactory.Img(_weaponRt, "GunBody", GunMetal);
        UIFactory.Pin(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(250f, 52f));

        var hi = UIFactory.Img(_weaponRt, "GunHighlight", GunLight);
        UIFactory.Pin(hi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 23f), new Vector2(250f, 8f));

        var bar = UIFactory.Img(_weaponRt, "Barrel", GunDark);
        UIFactory.Pin(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-175f, 14f), new Vector2(160f, 22f));
        _barrel = bar.rectTransform;

        var barHi = UIFactory.Img(_weaponRt, "BarrelTop", GunLight);
        UIFactory.Pin(barHi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-175f, 26f), new Vector2(160f, 6f));

        var sight = UIFactory.Img(_weaponRt, "Sight", GunDark);
        UIFactory.Pin(sight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-70f, 40f), new Vector2(34f, 22f));

        var mag = UIFactory.Img(_weaponRt, "Mag", GunDark);
        UIFactory.Pin(mag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(34f, -58f), new Vector2(52f, 96f));
        mag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        var grip = UIFactory.Img(_weaponRt, "Grip", GunDark);
        UIFactory.Pin(grip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-38f, -52f), new Vector2(42f, 74f));
        grip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);

        // 袖子 + 两只手，一眼看出是"人端着枪"
        var sleeve = UIFactory.Img(_weaponRt, "Sleeve", new Color(0.30f, 0.38f, 0.55f));
        UIFactory.Pin(sleeve.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-46f, -104f), new Vector2(84f, 40f));
        sleeve.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);

        var hand = UIFactory.Img(_weaponRt, "HandL", new Color(0.86f, 0.71f, 0.59f));
        UIFactory.Pin(hand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-152f, -20f), new Vector2(74f, 60f));
        hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        var hand2 = UIFactory.Img(_weaponRt, "HandR", new Color(0.83f, 0.68f, 0.56f));
        UIFactory.Pin(hand2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-44f, -62f), new Vector2(70f, 76f));

        // 枪口火光
        _muzzle = UIFactory.Img(_scene, "MuzzleFlash", new Color(1f, 0.92f, 0.55f, 0f));
        _muzzleRt = _muzzle.rectTransform;
        UIFactory.Pin(_muzzleRt, new Vector2(0.5f, 0.5f), MuzzleLocalPos(), new Vector2(96f, 96f));
        _muzzleRt.localRotation = Quaternion.Euler(0f, 0f, GunDeg);
        var core = UIFactory.Img(_muzzleRt, "Core", new Color(1f, 1f, 0.9f, 0.95f));
        UIFactory.Pin(core.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
        core.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    /// <summary>枪口在画布坐标系里的位置（跟着武器摆放和角度算出来）。</summary>
    static Vector2 MuzzleLocalPos()
    {
        float r = GunDeg * Mathf.Deg2Rad;
        return new Vector2(GunX + TipX * Mathf.Cos(r) - TipY * Mathf.Sin(r),
                           GunY + TipX * Mathf.Sin(r) + TipY * Mathf.Cos(r));
    }

    void BuildHud(RectTransform root)
    {
        // 左下：血量
        _hpBg = UIFactory.Img(root, "HpBg", new Color(0f, 0f, 0f, 0.55f));
        UIFactory.Pin(_hpBg.rectTransform, new Vector2(0f, 0f), new Vector2(22f, 80f), new Vector2(230f, 22f));
        _hpFill = UIFactory.Img(_hpBg.transform, "HpFill", new Color(0.35f, 0.85f, 0.45f));
        var hf = _hpFill.rectTransform;
        hf.anchorMin = new Vector2(0f, 0f); hf.anchorMax = new Vector2(1f, 1f);
        hf.offsetMin = new Vector2(2f, 2f); hf.offsetMax = new Vector2(-2f, -2f);

        _hpText = UIFactory.Label(root, "HpText", "HP 100", 20, new Color(0.85f, 1f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Pin(_hpText.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 104f), new Vector2(300f, 26f));

        // 右下：弹药 + 击杀
        _ammoText = UIFactory.Label(root, "Ammo", "", 40, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(-22f, 100f), new Vector2(320f, 54f));

        _killText = UIFactory.Label(root, "Kills", "", 22, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_killText.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 72f), new Vector2(320f, 26f));

        // 右上：比分
        _scoreText = UIFactory.Label(root, "Score", "", 22, new Color(0.95f, 0.7f, 0.35f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Pin(_scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(-22f, -56f), new Vector2(340f, 26f));

        // 换弹提示
        _reloadText = UIFactory.Label(root, "Reload", "", 30, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Pin(_reloadText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(500f, 40f));

        // 左上：击杀播报
        _feed = new Text[3];
        _feedLife = new float[3];
        _feedLine = new string[3];
        for (int i = 0; i < 3; i++)
        {
            _feed[i] = UIFactory.Label(root, "Feed" + i, "", 19, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft);
            UIFactory.Pin(_feed[i].rectTransform, new Vector2(0f, 1f), new Vector2(20f, -56f - i * 26f), new Vector2(520f, 24f));
        }

        // 浮动文字池
        _pops = new Popup[4];
        for (int i = 0; i < _pops.Length; i++)
        {
            var p = new Popup();
            p.txt = UIFactory.Label(root, "Pop" + i, "", 28, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
            p.rt = p.txt.rectTransform;
            UIFactory.Pin(p.rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 40f));
            p.t = -1f;
            p.txt.gameObject.SetActive(false);
            _pops[i] = p;
        }

        _ammoText.text = _ammo + " / " + (MagSize * 3);
        _hpText.text = "HP 100";
        _killText.text = "击杀 0";
        _scoreText.text = "回合 1  ·  0 : 0";
    }

    public int Kills { get { return _kills; } }
    public int Score { get { return _score; } }
    public int Hp { get { return _hp; } }

    public void ResetRound()
    {
        _ammo = MagSize; _kills = _score = 0; _hp = 100;
        _shake = _recoilX = _recoilY = _aimX = _aimY = 0f;
        _muzzleT = _tracerT = _hitT = _redT = _reloadT = _fireCd = 0f;
        _hpRegen = _hurtCd = _popupCd = _battleTime = 0f;
        _scene.anchoredPosition = Vector2.zero;
        _reloadText.text = "";
        _muzzleRt.gameObject.SetActive(false); _hitRt.gameObject.SetActive(false);
        for (int i = 0; i < Slots; i++)
        {
            var e = _enemies[i];
            e.state = 0; e.t = 0.4f + i * 0.35f;
            e.rt.localRotation = Quaternion.identity;
            SetEnemyAlpha(e, 0f); e.flash.color = Color.clear;
        }
        foreach (var pop in _pops) { pop.t = -1f; pop.txt.gameObject.SetActive(false); }
        System.Array.Clear(_feedLife, 0, _feedLife.Length);
        System.Array.Clear(_feedLine, 0, _feedLine.Length);
        TickFeed(0f);
        RefreshHud();
    }

    // ── 开关 ─────────────────────────────────────────────────
    public void SetActive(bool on)
    {
        _active = on;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        foreach (var c in GetComponentsInChildren<CanvasRenderer>(true)) c.SetAlpha(on ? 1f : 0f);
        gameObject.SetActive(on);
    }

    /// <summary>编辑器预览：摆一个"正在交火"的静态姿势。</summary>
    public void EditorPreview()
    {
        _active = false;
        gameObject.SetActive(true);
        _muzzleRt.gameObject.SetActive(true); _hitRt.gameObject.SetActive(true);
        for (int i = 0; i < Slots; i++)
        {
            var e = _enemies[i];
            bool up = i != 2;
            e.state = up ? 2 : 0;
            e.x = -300f + i * 200f;
            e.rt.anchoredPosition = new Vector2(e.x, up ? GroundY : RiseFrom);
            e.rt.localRotation = Quaternion.identity;
            e.rt.localScale = Vector3.one;
            SetEnemyAlpha(e, up ? 1f : 0f);
        }
        _crossRt.anchoredPosition = new Vector2(-100f, -18f);
        _hitMarker.color = new Color(1f, 0.35f, 0.3f, 0.9f);
        _muzzle.color = new Color(1f, 0.92f, 0.55f, 0.95f);
        _tracer.color = new Color(1f, 0.95f, 0.6f, 0f);
        _redFlash.color = new Color(1f, 0.1f, 0.08f, 0f);
        _ammo = 17; _kills = 12; _score = 1280; _hp = 74;
        RefreshHud();
        Kill("你", "AK-47", "敌人_03", true);
        Kill("你", "沙鹰", "靶子", false);
        // Update 在编辑器里不跑，这里手动把播报写出来
        for (int i = 0; i < _feed.Length; i++)
        {
            _feed[i].text = _feedLine[i] ?? "";
            _feed[i].color = new Color(0.95f, 0.97f, 1f, 0.92f);
        }
        _reloadText.text = "";
        for (int i = 0; i < _pops.Length; i++) { _pops[i].t = -1f; _pops[i].txt.gameObject.SetActive(false); }
        Popup2("爆头！", new Vector2(-100f, 20f));
        Popup2("+120", new Vector2(-100f, -14f));
    }

    // ── 每帧 ─────────────────────────────────────────────────
    void Update()
    {
        if (!_active || (SlackGame.I != null && SlackGame.I.State != SlackGame.GameState.Playing)) return;
        float dt = Time.deltaTime;
        _battleTime += dt;

        TickEnemies(dt);
        TickAimAndFire(dt);

        // 枪口火光 / 弹道 / 命中标记 / 受伤红闪 的衰减
        _muzzleT = Mathf.Max(0f, _muzzleT - dt);
        _muzzleRt.gameObject.SetActive(_muzzleT > 0f);
        _muzzle.color = new Color(1f, 0.92f, 0.55f, _muzzleT > 0f ? 0.95f : 0f);
        _muzzleRt.localScale = Vector3.one * (1f + (1f - _muzzleT / 0.06f) * 0.5f);

        _tracerT = Mathf.Max(0f, _tracerT - dt);
        var tc = _tracer.color; tc.a = Mathf.Clamp01(_tracerT / 0.05f) * 0.9f; _tracer.color = tc;

        _hitT = Mathf.Max(0f, _hitT - dt);
        _hitRt.gameObject.SetActive(_hitT > 0f);
        _hitMarker.color = new Color(1f, 0.35f, 0.3f, Mathf.Clamp01(_hitT / 0.12f) * 0.95f);
        _hitRt.localScale = Vector3.one * (1f + (1f - Mathf.Clamp01(_hitT / 0.12f)) * 0.6f);

        _redT = Mathf.Max(0f, _redT - dt);
        _redFlash.color = new Color(1f, 0.1f, 0.08f, Mathf.Clamp01(_redT / 0.35f) * 0.35f);

        // 开火抖动
        _shake = Mathf.Max(0f, _shake - dt * 6f);
        if (_scene != null)
        {
            float s = _shake * 7f;
            _scene.anchoredPosition = new Vector2(Random.Range(-s, s), Random.Range(-s, s));
        }

        TickPops(dt);
        TickFeed(dt);
        RefreshHud();
    }

    void TickEnemies(float dt)
    {
        for (int i = 0; i < Slots; i++)
        {
            var e = _enemies[i];
            e.t -= dt;
            switch (e.state)
            {
                case 0: // 躲着
                    if (e.t <= 0f)
                    {
                        e.state = 1;
                        e.t = 0.16f;
                        e.x = Random.Range(-390f, 390f);
                        e.rt.anchoredPosition = new Vector2(e.x, RiseFrom);
                        e.rt.localRotation = Quaternion.identity;
                        e.rt.localScale = Vector3.one;
                        SetEnemyAlpha(e, 1f);
                    }
                    break;

                case 1: // 冒头
                {
                    float k = 1f - Mathf.Clamp01(e.t / 0.16f);
                    e.rt.anchoredPosition = new Vector2(e.x, Mathf.Lerp(RiseFrom, GroundY, k));
                    if (e.t <= 0f) { e.state = 2; e.t = Random.Range(1.2f, 3.0f); }
                    break;
                }

                case 2: // 站着（会轻微晃动，并随机"还击"打你）
                {
                    float bob = Mathf.Sin(Time.time * 6f + i) * 2.5f;
                    e.rt.anchoredPosition = new Vector2(e.x + bob, GroundY);
                    if (Random.value < dt * 0.35f && _hurtCd <= 0f)
                    {
                        e.flash.color = new Color(1f, 0.85f, 0.4f, 0.85f);
                        _hp = Mathf.Max(0, _hp - Random.Range(6, 15));
                        _redT = 0.35f;
                        _hurtCd = 1.6f;
                        if (_hp <= 0)
                        {
                            _hp = 100;
                            Popup2("你被击倒了…复活", new Vector2(0f, 40f));
                        }
                    }
                    if (e.t <= 0f) { e.state = 0; e.t = Random.Range(0.3f, 1.1f); SetEnemyAlpha(e, 0f); }
                    break;
                }

                case 3: // 倒下
                {
                    float k = 1f - Mathf.Clamp01(e.t / 0.28f);
                    e.rt.localRotation = Quaternion.Euler(0f, 0f, -78f * k);
                    e.rt.anchoredPosition = new Vector2(e.x - 26f * k, GroundY - 18f * k);
                    e.rt.GetComponent<CanvasGroup>().alpha = 1f - k;
                    var c = e.body.color; c.a = 1f - k; e.body.color = c;
                    var c2 = e.head.color; c2.a = 1f - k; e.head.color = c2;
                    if (e.t <= 0f) { e.state = 0; e.t = Random.Range(0.25f, 0.9f); SetEnemyAlpha(e, 0f); }
                    break;
                }
            }

            // 敌人火光衰减
            var fc = e.flash.color;
            if (fc.a > 0f) { fc.a = Mathf.Max(0f, fc.a - dt * 4f); e.flash.color = fc; }
        }

        _hurtCd = Mathf.Max(0f, _hurtCd - dt);
        _hpRegen += dt * 2f;
        if (_hpRegen >= 1f) { int heal = Mathf.FloorToInt(_hpRegen); _hp = Mathf.Min(100, _hp + heal); _hpRegen -= heal; }
    }

    void SetEnemyAlpha(Enemy e, float a)
    {
        e.rt.GetComponent<CanvasGroup>().alpha = a;
        var b = e.body.color; b.a = a; e.body.color = b;
        var h = e.head.color; h.a = a; e.head.color = h;
    }

    void TickAimAndFire(float dt)
    {
        // 找目标：优先打离准心最近的站着的人
        Enemy target = null;
        float best = float.MaxValue;
        for (int i = 0; i < Slots; i++)
        {
            var e = _enemies[i];
            if (e.state != 2) continue;
            float d = Mathf.Abs(e.x - _aimX);
            if (d < best) { best = d; target = e; }
        }

        // 换弹
        if (_reloadT > 0f)
        {
            _reloadT -= dt;
            _reloadText.text = "换弹中… " + Mathf.CeilToInt(Mathf.Max(0f, _reloadT) * 10f) / 10f + "s";
            if (_reloadT <= 0f) { _ammo = MagSize; _reloadText.text = ""; }
            _recoilX = Mathf.Lerp(_recoilX, 0f, 1f - Mathf.Exp(-8f * dt));
            _recoilY = Mathf.Lerp(_recoilY, 0f, 1f - Mathf.Exp(-8f * dt));
            ApplyCross();
            return;
        }

        // 准心移动：有目标就压过去，没有就慢慢游走
        float tx, ty;
        if (target != null)
        {
            tx = target.x;
            ty = GroundY + 30f + Mathf.Sin(Time.time * 3.1f) * 4f;
        }
        else
        {
            tx = Mathf.Sin(Time.time * 0.9f) * 260f;
            ty = -10f + Mathf.Sin(Time.time * 1.7f) * 30f;
        }
        float sp = target != null ? 9f : 2.5f;
        _aimX = Mathf.Lerp(_aimX, tx, 1f - Mathf.Exp(-sp * dt));
        _aimY = Mathf.Lerp(_aimY, ty, 1f - Mathf.Exp(-sp * dt));

        _recoilX = Mathf.Lerp(_recoilX, 0f, 1f - Mathf.Exp(-11f * dt));
        _recoilY = Mathf.Lerp(_recoilY, 0f, 1f - Mathf.Exp(-11f * dt));
        ApplyCross();

        // 开火
        _fireCd -= dt;
        if (target != null && Mathf.Abs(target.x - _aimX) < 16f && _fireCd <= 0f)
        {
            _fireCd = Random.Range(0.22f, 0.44f);
            Fire(target);
        }
        _popupCd = Mathf.Max(0f, _popupCd - dt);
    }

    void ApplyCross()
    {
        _crossRt.anchoredPosition = new Vector2(_aimX + _recoilX, _aimY + _recoilY);
    }

    void Fire(Enemy target)
    {
        _ammo--;
        _muzzleT = 0.06f;
        _shake = 1f;
        _recoilY += 7f;
        _recoilX += Random.Range(-3.5f, 3.5f);

        // 弹道
        Vector2 from = MuzzleLocalPos();
        Vector2 to = new Vector2(target.x, GroundY + 30f);
        Vector2 d = to - from;
        _tracerRt.sizeDelta = new Vector2(d.magnitude, 3f);
        _tracerRt.anchoredPosition = (from + to) * 0.5f;
        _tracerRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        _tracerT = 0.05f;

        // 击杀
        target.state = 3;
        target.t = 0.28f;
        _kills++;
        bool head = Random.value < 0.35f;
        _score += head ? 120 : 100;
        _hitT = 0.12f;
        _hitRt.anchoredPosition = to;

        Vector2 p = new Vector2(to.x, to.y + 30f);
        Popup2(head ? "爆头！" : "+100", p);
        if (Random.value < 0.22f) Popup2(Meme.Pick(Meme.ShooterTaunts), p + new Vector2(0f, 30f));

        Kill("你", Meme.Pick(Meme.ShooterWeapons), Meme.Pick(Meme.ShooterTargets), head);

        if (_ammo <= 0) { _reloadT = 1.2f; }
    }

    void Popup2(string text, Vector2 at)
    {
        for (int i = 0; i < _pops.Length; i++)
        {
            if (_pops[i].t > 0f) continue;
            var p = _pops[i];
            p.txt.text = text;
            p.txt.gameObject.SetActive(true);
            p.from = at;
            p.rt.anchoredPosition = at;
            p.t = 0.9f; p.dur = 0.9f;
            p.txt.color = text.StartsWith("爆") || text.StartsWith("双") || text.StartsWith("三")
                ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.95f, 0.6f);
            return;
        }
    }

    public void Kill(string who, string weapon, string victim, bool head)
    {
        string line = who + "  [" + weapon + "]" + (head ? "  ◎ " : "  →  ") + victim;
        for (int i = _feed.Length - 1; i > 0; i--)
        {
            _feedLine[i] = _feedLine[i - 1];
            _feedLife[i] = _feedLife[i - 1];
        }
        _feedLine[0] = line;
        _feedLife[0] = 5.5f;
    }

    void TickFeed(float dt)
    {
        for (int i = 0; i < _feed.Length; i++)
        {
            if (_feedLife[i] > 0f) _feedLife[i] -= dt;
            _feed[i].text = _feedLife[i] > 0f ? _feedLine[i] : "";
            float a = Mathf.Clamp01(_feedLife[i] / 1.2f);
            _feed[i].color = new Color(0.95f, 0.97f, 1f, a * 0.92f);
        }
    }

    void TickPops(float dt)
    {
        for (int i = 0; i < _pops.Length; i++)
        {
            var p = _pops[i];
            if (p.t <= 0f) continue;
            p.t -= dt;
            float k = 1f - Mathf.Clamp01(p.t / p.dur);
            p.rt.anchoredPosition = p.from + new Vector2(0f, k * 26f);
            var c = p.txt.color; c.a = 1f - k * k; p.txt.color = c;
            if (p.t <= 0f) p.txt.gameObject.SetActive(false);
        }
    }

    void RefreshHud()
    {
        _ammoText.text = _ammo + " / " + (MagSize * 3);
        _ammoText.color = _ammo <= 6 ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.92f, 0.55f);
        _killText.text = "击杀 " + _kills;
        _scoreText.text = "比分 " + _score + "  ·  K/D " + _kills + "/" + Mathf.Max(0, _kills / 5);
        _hpText.text = "HP " + _hp;
        _hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_hp / 100f), 1f);
        _hpFill.color = _hp > 50 ? new Color(0.35f, 0.85f, 0.45f)
                     : _hp > 25 ? new Color(0.95f, 0.75f, 0.3f)
                                : new Color(0.95f, 0.3f, 0.28f);

    }
}
