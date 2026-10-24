using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时搭 UI 的小工具：中文字体、Canvas、Text、Image、程序化贴图。
/// </summary>
public static class UIFactory
{
    static Font _cjk;

    /// <summary>从系统字体里挑一个带中文的（macOS: PingFang SC）。</summary>
    public static Font Cjk
    {
        get
        {
            if (_cjk != null) return _cjk;
            string[] candidates =
            {
                "PingFang SC", "Hiragino Sans GB", "Heiti SC", "STHeiti",
                "Microsoft YaHei", "SimHei", "Noto Sans CJK SC",
                "Source Han Sans SC", "Arial Unicode MS", "Songti SC",
            };
            try { _cjk = Font.CreateDynamicFontFromOSFont(candidates, 44); } catch { }
            if (_cjk == null) { try { _cjk = Font.CreateDynamicFontFromOSFont("Arial", 44); } catch { } }
            if (_cjk == null)
            {
                try { _cjk = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_cjk == null) { try { _cjk = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            }
            return _cjk;
        }
    }

    static Sprite _rounded;
    public static Image Panel(Transform parent, string name, Color color)
    {
        if (_rounded == null)
        {
            const int size = 64; const float radius = 12f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (32f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (32f - radius), 0f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            tex.SetPixels(pixels); tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        }
        var image = Img(parent, name, color);
        image.sprite = _rounded;
        image.type = Image.Type.Sliced;
        return image;
    }

    // ── Canvas ───────────────────────────────────────────────

    /// <summary>
    /// 全屏 HUD 画布。挂在相机上（ScreenSpaceCamera），
    /// 这样画布尺寸永远跟着相机渲染分辨率走，不受 Game View 窗口比例影响。
    /// </summary>
    public static Canvas ScreenCanvas(Transform parent, string name, int sortOrder, Camera cam)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var c = go.GetComponent<Canvas>();
        if (cam != null)
        {
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 0.55f;
        }
        else c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortOrder;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1600f, 900f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        return c;
    }

    public static Canvas WorldCanvas(Transform parent, string name, Vector2 size, Vector3 pos, Vector3 euler, float metersWide)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localPosition = pos;
        rt.localRotation = Quaternion.Euler(euler);
        rt.localScale = Vector3.one * (metersWide / size.x);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 0;
        return c;
    }

    // ── 基础控件 ─────────────────────────────────────────────

    public static RectTransform Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Img(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    public static Text Label(Transform parent, string name, string content, int size, Color color,
                             TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Cjk;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = false;
        return t;
    }

    /// <summary>把 RectTransform 拉伸铺满父级，四边留 pad 像素。</summary>
    public static RectTransform Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    /// <summary>锚定到父级某条边/角。anchor 用 (0,1)=左上之类。</summary>
    public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // ── 程序化贴图 ───────────────────────────────────────────

    static Sprite ToSprite(Texture2D tex)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>表格网格，用来当 Excel 背景。注意：整张铺满，不要用 Tiled。</summary>
    public static Sprite Grid(int w, int h, Color bg, Color line, int step, int thickness = 2)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        var px = new Color32[w * h];
        Color32 cb = bg, cl = line;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = (x % step < thickness || y % step < thickness) ? cl : cb;
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    /// <summary>噪点/色块，用来当"视频播放中"的背景。低分辨率 + Point 过滤 = 大色块。</summary>
    public static Sprite Noise(int w, int h, Color a, Color b, int seed)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        var rnd = new System.Random(seed);
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            float k = (float)rnd.NextDouble();
            px[i] = Color.Lerp(a, b, k * k);
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    /// <summary>四周发散的红光晕，危险时闪。</summary>
    public static Sprite Vignette(int size, Color edge)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421356f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1.0f, d));
                px[y * size + x] = new Color(edge.r, edge.g, edge.b, a * edge.a);
            }
        }
        tex.SetPixels(px);
        return ToSprite(tex);
    }
}
