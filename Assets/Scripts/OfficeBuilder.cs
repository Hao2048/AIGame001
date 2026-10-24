using UnityEngine;

/// <summary>
/// 纯代码搭建整个办公室：房间、工位、显示器、老板模型、灯光、相机。
/// 所有比例都按"人坐在工位前、相机在眼位"来配。
/// </summary>
public static class OfficeBuilder
{
    // ── 关键布局常量（调手感就改这里）────────────────────────
    public static readonly Vector3 CamPos = new Vector3(0f, 1.60f, -1.05f);
    public static readonly Vector3 CamLook = new Vector3(0f, 1.35f, 0.02f);
    public const float Fov = 68f;

    public static readonly Vector3 MonitorCenter = new Vector3(0f, 1.30f, 0.05f);
    public const float MonitorWidth = 1.10f;
    public const float MonitorHeight = 0.62f;

    /// <summary>老板巡逻所在的 z（就在工位后方，从显示器边缘冒出来）。</summary>
    public const float CorridorZ = 1.60f;
    public const float PatrolX = 3.15f;

    public static Camera Cam;
    public static GameObject Root;
    public static ScreenDisplay Screen;
    public static HudUI Hud;
    public static BossController Boss;
    public static Transform BossRoot;
    public static Transform BossBody;
    public static Transform BossBubblePivot;
    public static Transform BossAlertPivot;
    public static UnityEngine.UI.Text BossBubble;
    public static UnityEngine.UI.Text BossAlert;
    public static Light MonitorGlow;
    static Mesh _beveledCube;

    /// <summary>运行时用 Destroy，编辑模式用 DestroyImmediate。</summary>
    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    static Transform NewChild(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Root.transform, false);
        return go.transform;
    }

    // ── 材质 ─────────────────────────────────────────────────
    static Material Mat(Color c, float smooth = 0.25f, float metal = 0f)
    {
        return RealismLook.Flat(c);
    }

    static Material Emissive(Color c, float power = 1.6f)
    {
        var m = Mat(c, 0.1f);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * power);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    // ── 图元 ─────────────────────────────────────────────────
    static GameObject Prim(PrimitiveType type, Transform parent, string name,
                           Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var col = go.GetComponent<Collider>();
        if (col != null) Kill(col);
        if (type == PrimitiveType.Cube)
        {
            if (_beveledCube == null)
            {
                var unit = Resources.Load<GameObject>("Realism/BevelledUnit");
                if (unit != null) _beveledCube = unit.GetComponentInChildren<MeshFilter>().sharedMesh;
            }
            if (_beveledCube != null) go.GetComponent<MeshFilter>().sharedMesh = _beveledCube;
        }
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }

    static GameObject Box(Transform p, string n, Vector3 pos, Vector3 scale, Material m)
        => Prim(PrimitiveType.Cube, p, n, pos, scale, m);

    static GameObject Ball(Transform p, string n, Vector3 pos, Vector3 scale, Material m)
        => Prim(PrimitiveType.Sphere, p, n, pos, scale, m);

    static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 scale, Material m)
        => Prim(PrimitiveType.Cylinder, p, n, pos, scale, m);

    // ── 入口 ─────────────────────────────────────────────────
    /// <summary>
    /// 生成整个办公室。所有东西都挂在 "GameRoot" 下，重复调用会先清掉旧的，
    /// 所以运行时和编辑器里用的是同一套代码，不会出现双份。
    /// </summary>
    public static void BuildWorld(bool useBuiltInCharacter = false)
    {
        ClearExisting();
        Root = new GameObject("GameRoot");
        SetupCamera();
        SetupLighting();
        BuildRoom();
        BuildDesk();
        Screen = ScreenDisplay.Create(BuildMonitor());
        BuildBoss(useBuiltInCharacter);
        BuildProps();
        BuildDetails();
        RealismLook.Configure(Cam, Root.transform);
        Hud = HudUI.Create(Root.transform);
    }

    static void ClearExisting()
    {
        // 必须立刻销毁，不能延迟到帧末：否则旧的 BossController / ScreenDisplay / HudUI
        // 会活到这一帧结束，而紧接着就创建新的一批，脚本若按类型查找就会抓到"即将被销毁"的旧对象。
        var old = GameObject.Find("GameRoot");
        if (old != null) Object.DestroyImmediate(old);

        foreach (var c in Object.FindObjectsByType<Camera>())
            if (c.gameObject.activeSelf) c.gameObject.SetActive(false);
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.gameObject.activeSelf) l.gameObject.SetActive(false);
        foreach (var al in Object.FindObjectsByType<AudioListener>())
            Kill(al);
    }

    static void SetupCamera()
    {
        var go = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        go.transform.SetParent(Root.transform, false);
        go.transform.localPosition = CamPos;
        go.transform.localRotation = Quaternion.LookRotation(CamLook - CamPos, Vector3.up);
        var cam = go.GetComponent<Camera>();
        cam.fieldOfView = Fov;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.58f,.73f,.84f);
        cam.allowHDR = true;
        Cam = cam;
    }

    static void SetupLighting()
    {
        var sun = new GameObject("Sun", typeof(Light));
        sun.transform.SetParent(Root.transform, false);
        var l = sun.GetComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, .96f, .87f);
        l.intensity = 1.55f;
        l.shadowBias = .025f; l.shadowNormalBias = .18f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.82f;
        sun.transform.rotation = Quaternion.Euler(48f, -52f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.33f, 0.39f, 0.43f);
        RenderSettings.fog = false;
    }

    static void BuildRoom()
    {
        var root = NewChild("Room");

        var carpet = RealismLook.Surface("Carpet");
        var wall = RealismLook.Surface("Plaster");
        var wallDark = Mat(new Color(.28f,.39f,.48f), 0.08f);
        var ceiling = Mat(new Color(.80f,.86f,.85f), 0.05f);
        ceiling.SetColor("_ShadowColor",new Color(.88f,.92f,1f));
        ceiling.SetColor("_EmissionColor",new Color(.13f,.15f,.16f));

        Box(root, "Floor", new Vector3(0f, -0.10f, 2f), new Vector3(40f, 0.2f, 30f), carpet);
        Box(root, "Ceiling", new Vector3(0f, 3.52f, 2f), new Vector3(40f, 0.2f, 30f), ceiling);
        Box(root, "WallBack", new Vector3(0f, 1.7f, 8.1f), new Vector3(40f, 3.6f, 0.2f), wall);
        Box(root, "WallFront", new Vector3(0f, 1.7f, -6.1f), new Vector3(40f, 3.6f, 0.2f), wall);
        Box(root, "WallLeft", new Vector3(-11.1f, 1.7f, 1f), new Vector3(0.2f, 3.6f, 30f), wall);
        Box(root, "WallRight", new Vector3(11.1f, 1.7f, 1f), new Vector3(0.2f, 3.6f, 30f), wall);

        // 踢脚线，让房间有点层次
        Box(root, "SkirtingBack", new Vector3(0f, 0.06f, 7.97f), new Vector3(40f, 0.14f, 0.08f), wallDark);

        // 顶部灯带
        var panel = Emissive(new Color(1f, 0.98f, 0.93f), 1.4f);
        for (int i = 0; i < 3; i++)
        {
            Box(root, "LightPanel" + i, new Vector3(-5f + i * 5f, 3.40f, 2f), new Vector3(3.2f, 0.06f, 1.2f), panel);
            var pl = new GameObject("CeilingLight" + i, typeof(Light));
            pl.transform.SetParent(root, false);
            pl.transform.localPosition = new Vector3(-5f + i * 5f, 3.2f, 2f);
            var li = pl.GetComponent<Light>();
            li.type = LightType.Point;
            li.range = 9f;
            li.intensity = 6f;
            li.color = new Color(1f, 0.97f, 0.92f);
            li.shadows = LightShadows.None;
        }
    }

    static void BuildDesk()
    {
        var root = NewChild("Workstation");
        if (RealismLook.Model("OfficeDesk", root, Vector3.zero) != null)
        {
            RealismLook.Model("OfficePlant",root,new Vector3(-.99f,.758f,.14f),.22f);
            return;
        }

        var deskTop = RealismLook.Surface("Walnut");
        var metal = Mat(new Color(0.22f, 0.23f, 0.26f), 0.65f, 0.55f);
        var paper = Mat(new Color(0.93f, 0.92f, 0.88f), 0.05f);

        Box(root, "DeskTop", new Vector3(0f, 0.72f, -0.25f), new Vector3(2.60f, 0.06f, 1.15f), deskTop);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(root, "Leg", new Vector3(sx * 1.22f, 0.36f, -0.25f + sz * 0.47f),
                    new Vector3(0.07f, 0.72f, 0.07f), metal);

        // 键盘 / 鼠标
        Box(root, "Keyboard", new Vector3(0f, 0.762f, -0.62f), new Vector3(0.95f, 0.022f, 0.32f),
            Mat(new Color(0.13f, 0.14f, 0.16f), 0.4f));
        Ball(root, "Mouse", new Vector3(0.74f, 0.778f, -0.60f), new Vector3(0.075f, 0.04f, 0.11f),
            Mat(new Color(0.16f, 0.17f, 0.19f), 0.5f));

        // 马克杯 + 便签堆
        var mug = Mat(new Color(0.85f, 0.28f, 0.24f), 0.4f);
        Cyl(root, "Mug", new Vector3(-0.96f, 0.80f, -0.48f), new Vector3(0.09f, 0.05f, 0.09f), mug);
        Box(root, "Papers", new Vector3(1.02f, 0.775f, -0.50f), new Vector3(0.30f, 0.035f, 0.42f), paper);
        Box(root, "Papers2", new Vector3(1.05f, 0.795f, -0.46f), new Vector3(0.27f, 0.03f, 0.38f), paper);
    }

    /// <summary>显示器本体，返回屏幕画布该挂的位置。</summary>
    static Transform BuildMonitor()
    {
        var root = NewChild("Monitor");
        var imported = RealismLook.Model("OfficeMonitor",root,Vector3.zero);
        if (imported == null) {
        var bezel = Mat(new Color(0.035f, 0.055f, 0.065f), 0.35f);
        var stand = Mat(new Color(0.18f, 0.19f, 0.22f), 0.6f, 0.5f);

        Box(root, "Bezel", new Vector3(MonitorCenter.x, MonitorCenter.y, MonitorCenter.z + 0.035f),
            new Vector3(MonitorWidth + 0.08f, MonitorHeight + 0.08f, 0.05f), bezel);
        Box(root, "ScreenSurface", new Vector3(MonitorCenter.x, MonitorCenter.y, MonitorCenter.z + 0.005f),
            new Vector3(MonitorWidth, MonitorHeight, 0.012f), Emissive(new Color(0.10f, 0.13f, 0.18f), 0.8f));

        float bottom = MonitorCenter.y - MonitorHeight * 0.5f - 0.04f;
        Cyl(root, "Neck", new Vector3(0f, (0.775f + bottom) * 0.5f, 0.06f),
            new Vector3(0.09f, (bottom - 0.775f) * 0.5f, 0.09f), stand);
        Box(root, "Base", new Vector3(0f, 0.762f, 0.06f), new Vector3(0.55f, 0.025f, 0.26f), stand);

        }
        // 屏幕打在桌面上的光晕
        var glowGo = new GameObject("MonitorGlow", typeof(Light));
        glowGo.transform.SetParent(root, false);
        glowGo.transform.localPosition = new Vector3(0f, 0.95f, -0.42f);
        MonitorGlow = glowGo.GetComponent<Light>();
        MonitorGlow.type = LightType.Point;
        MonitorGlow.range = 2.2f;
        MonitorGlow.intensity = 1.5f;
        MonitorGlow.color = new Color(0.55f, 0.85f, 1f);
        MonitorGlow.shadows = LightShadows.None;

        return root;
    }

    // ── 老板 ─────────────────────────────────────────────────
    static void BuildBoss(bool useBuiltInCharacter)
    {
        var root = NewChild("Boss");
        BossRoot = root;
        root.localPosition = new Vector3(PatrolX, 0f, CorridorZ);
        var body = new GameObject("Body").transform;
        body.SetParent(root, false);
        BossBody = body;
        var character = useBuiltInCharacter ? null : RealismLook.Model("Renderpeople/Eric", body, Vector3.zero, .98f);
        if (character == null) BuiltInBoss.Create(body);
        RealismLook.GroundContact(root);
        var motion = body.gameObject.AddComponent<CharacterMotion>();

        // 头顶气泡
        var bubbleCanvas = UIFactory.WorldCanvas(root, "BossBubble", new Vector2(320f, 96f),
            new Vector3(0f, 2.12f, 0f), Vector3.zero, 0.92f);
        BossBubblePivot = bubbleCanvas.transform;
        var bbg = UIFactory.Img(bubbleCanvas.transform, "BubbleBg", new Color(1f, 1f, 1f, 0.0f));
        UIFactory.Stretch(bbg.rectTransform);
        BossBubble = UIFactory.Label(bubbleCanvas.transform, "BubbleText", "", 46,
            new Color(0.15f, 0.15f, 0.18f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(BossBubble.rectTransform);
        BossBubblePivot.gameObject.AddComponent<Billboard>().cam = Cam;

        // 头顶警报
        var alertCanvas = UIFactory.WorldCanvas(root, "BossAlert", new Vector2(200f, 200f),
            new Vector3(0f, 2.20f, 0f), Vector3.zero, 0.75f);
        BossAlertPivot = alertCanvas.transform;
        BossAlert = UIFactory.Label(alertCanvas.transform, "AlertText", "", 150,
            new Color(1f, 0.25f, 0.20f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(BossAlert.rectTransform);
        BossAlertPivot.gameObject.AddComponent<Billboard>().cam = Cam;
        BossAlert.gameObject.SetActive(false);

        // 控制器就挂在老板身上，整个 GameRoot 被销毁时它跟着一起走
        Boss = root.gameObject.AddComponent<BossController>();
        Boss.Init(null, BossBody, BossBubblePivot, BossBubble, BossAlertPivot, BossAlert, Cam);
        motion.Initialize(Boss);
    }

    // ── 背景道具（让办公室像办公室）──────────────────────────
    static void BuildProps()
    {
        var root = NewChild("Props");
        var wood = RealismLook.Surface("Walnut");
        var glass = Mat(new Color(0.30f, 0.45f, 0.55f), 0.85f);
        var leaf = Mat(new Color(0.22f, 0.48f, 0.26f), 0.15f);
        var pot = Mat(new Color(0.72f, 0.38f, 0.26f), 0.20f);
        var metal = Mat(new Color(0.55f, 0.57f, 0.60f), 0.7f, 0.6f);
        var paper = Mat(new Color(0.90f, 0.89f, 0.85f), 0.05f);

        // 老板办公室的门（红字招牌）
        Box(root, "BossDoor", new Vector3(3.4f, 1.12f, 7.98f), new Vector3(1.55f, 2.24f, 0.10f), wood);
        Box(root, "BossDoorFrame", new Vector3(3.4f, 1.16f, 7.94f), new Vector3(1.78f, 2.40f, 0.06f),
            Mat(new Color(0.20f, 0.21f, 0.24f), 0.4f));
        Box(root, "BossDoorPanel", new Vector3(3.4f, 1.12f, 7.91f), new Vector3(1.36f, 2.04f, 0.04f), wood);
        Box(root, "BossSign", new Vector3(3.4f, 2.52f, 7.90f), new Vector3(1.0f, 0.24f, 0.06f),
            Emissive(new Color(1f, 0.15f, 0.12f), 2.2f));
        Ball(root, "DoorKnob", new Vector3(2.80f, 1.10f, 7.88f), new Vector3(0.08f, 0.08f, 0.08f), metal);

        // 电梯门
        Box(root, "Lift", new Vector3(-3.6f, 1.10f, 7.96f), new Vector3(2.0f, 2.2f, 0.10f), metal);
        Box(root, "LiftSeam", new Vector3(-3.6f, 1.10f, 7.90f), new Vector3(0.04f, 2.2f, 0.06f),
            Mat(new Color(0.30f, 0.31f, 0.33f), 0.5f));

        // 饮水机
        Box(root, "Cooler", new Vector3(-6.4f, 0.55f, 5.6f), new Vector3(0.44f, 1.10f, 0.44f), Mat(new Color(0.85f, 0.86f, 0.88f), 0.4f));
        Cyl(root, "Bottle", new Vector3(-6.4f, 1.34f, 5.6f), new Vector3(0.36f, 0.24f, 0.36f), glass);

        // 绿植
        float[] px = { -7.6f, 7.6f, -7.6f };
        float[] pz = { 6.9f, 6.9f, 1.2f };
        for (int i = 0; i < 3; i++)
        {
            if (RealismLook.Model("OfficePlant",root,new Vector3(px[i],0f,pz[i])) != null) continue;
            Cyl(root, "Pot" + i, new Vector3(px[i], 0.24f, pz[i]), new Vector3(0.42f, 0.24f, 0.42f), pot);
            Ball(root, "Bush" + i, new Vector3(px[i], 0.78f, pz[i]), new Vector3(0.85f, 0.95f, 0.85f), leaf);
            Ball(root, "Bush2" + i, new Vector3(px[i] + 0.16f, 1.14f, pz[i] - 0.10f), new Vector3(0.55f, 0.60f, 0.55f), leaf);
        }

        // 远处的同事工位（剪影，营造"办公室"感）
        for (int i = 0; i < 4; i++)
        {
            float x = -5.5f + i * 3.6f;
            float z = 5.0f;
            RealismLook.Model("OfficeChair",root,new Vector3(x,0f,6.0f));
            Box(root, "ColDesk" + i, new Vector3(x, 0.70f, z), new Vector3(2.2f, 0.06f, 1.0f), wood);
            Box(root, "ColMon" + i, new Vector3(x, 1.05f, z + 0.2f), new Vector3(0.95f, 0.56f, 0.06f),
                Mat(new Color(0.10f, 0.11f, 0.13f), 0.3f));
            Box(root, "ColPaper" + i, new Vector3(x - 0.6f, 0.745f, z - 0.2f), new Vector3(0.3f, 0.02f, 0.4f), paper);
        }

        // 玻璃隔断
        var part = RealismLook.Glass();
        Box(root, "Partition", new Vector3(0f, 0.48f, 3.4f), new Vector3(18f, 0.96f, 0.06f), part);
        Box(root, "PartitionTop", new Vector3(0f, 0.99f, 3.4f), new Vector3(18f, 0.08f, 0.14f), metal);
        for (int i = -3; i <= 3; i++)
            Box(root, "PartitionPost", new Vector3(i * 3f, 0.5f, 3.4f), new Vector3(0.07f, 1f, 0.10f), metal);
    }
    static void WorldLabel(Transform parent, string name, string text, Vector3 pos,
                           float width, Color color, int size = 40)
    {
        var canvas = UIFactory.WorldCanvas(parent, name, new Vector2(600f, 180f), pos, Vector3.zero, width);
        var label = UIFactory.Label(canvas.transform, "Text", text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(label.rectTransform);
    }

    static void BuildDetails()
    {
        var root = NewChild("OfficeDetails");
        var ink = Mat(new Color(0.055f, 0.10f, 0.12f), 0.3f);
        var brass = Mat(new Color(0.77f, 0.51f, 0.24f), 0.65f, 0.5f);
        var cream = Mat(new Color(0.9f, 0.86f, 0.70f), 0.15f);
        var green = Mat(new Color(0.12f, 0.32f, 0.23f), 0.2f);
        var teal = Mat(new Color(0.15f, 0.35f, 0.36f));

        // Large windows and a layered skyline replace the flat background.
        for (int i = 0; i < 3; i++)
        {
            float x = -5.8f + i * 2.5f;
            Box(root, "WindowFrame", new Vector3(x, 2.0f, 7.83f), new Vector3(2.28f, 2.35f, 0.15f), ink);
            Box(root, "EveningSky", new Vector3(x, 2.03f, 7.72f), new Vector3(2.1f, 2.12f, 0.04f),
                Emissive(new Color(.52f,.76f,.96f), .15f));
            for (int b = 0; b < 5; b++)
            {
                float height = 0.3f + Mathf.Abs(Mathf.Sin(i * 2.7f + b * 4.3f)) * 0.7f;
                Box(root, "Skyline", new Vector3(x - 0.84f + b * 0.42f, 1.04f + height / 2f, 7.65f),
                    new Vector3(0.34f, height, 0.04f), teal);
            }
            var cloud = Mat(new Color(.96f,.98f,1f));
            for (int puff=0;puff<3;puff++)
                Ball(root,"PaintedCloud",new Vector3(x-.55f+puff*.25f,2.72f+Mathf.Sin(puff*1.5f)*.10f,7.66f),
                    new Vector3(.50f,.20f,.012f),cloud);
            Box(root, "WindowMullion", new Vector3(x, 2.03f, 7.59f), new Vector3(0.055f, 2.15f, 0.06f), ink);
            Box(root, "WindowCrossbar", new Vector3(x, 2.3f, 7.59f), new Vector3(2.1f, 0.055f, 0.06f), ink);
            Box(root, "WindowSill", new Vector3(x, 0.94f, 7.5f), new Vector3(2.4f, 0.08f, 0.38f), cream);
        }

        Box(root, "OfficeBrandPanel", new Vector3(3.4f, 2.75f, 7.79f), new Vector3(3.1f, 0.65f, 0.08f), ink);
        WorldLabel(root, "OfficeBrand", "M O Y U   /   摸鱼事务所", new Vector3(3.4f, 2.75f, 7.73f), 2.8f,
            new Color(0.94f, 0.82f, 0.57f), 36);
        WorldLabel(root, "DoorTitle", "BOSS  /  经理室", new Vector3(3.4f, 1.85f, 7.86f), 1.1f, Color.white, 40);
        Box(root, "PosterFrame", new Vector3(6.2f, 2.1f, 7.78f), new Vector3(1.5f, 1.6f, 0.08f), brass);
        Box(root, "PosterPaper", new Vector3(6.2f, 2.1f, 7.71f), new Vector3(1.36f, 1.46f, 0.04f), cream);
        WorldLabel(root, "PosterType", "WORK LESS\n摸鱼有度\n快乐下班", new Vector3(6.2f, 2.12f, 7.65f), 1.22f,
            new Color(0.08f, 0.2f, 0.22f), 45);

        // Carpet tile seams give the corridor a readable ground plane.
        var seam = Mat(new Color(.31f,.43f,.46f), 0.05f);
        for (int i = -7; i <= 7; i++)
            Box(root, "CarpetSeam", new Vector3(i * 1.2f, 0.008f, 3f), new Vector3(0.012f, 0.005f, 12f), seam);
        for (int i = 0; i < 8; i++)
            Box(root, "CarpetSeam", new Vector3(0f, 0.009f, i * 1.2f), new Vector3(18f, 0.005f, 0.012f), seam);

        // Power LED and tactile frame trim.
        Ball(root, "MonitorLed", new Vector3(0.48f, MonitorCenter.y - MonitorHeight / 2f - 0.02f, 0.048f),
            Vector3.one * 0.012f, Emissive(new Color(0.3f, 0.95f, 0.72f), 1.5f));
        Box(root, "MonitorTrim", new Vector3(0f, MonitorCenter.y - MonitorHeight / 2f - 0.035f, 0.047f),
            new Vector3(0.55f, 0.006f, 0.008f), brass);
    }

}
