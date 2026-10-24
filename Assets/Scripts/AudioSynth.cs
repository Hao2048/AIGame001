using UnityEngine;

/// <summary>
/// 纯代码合成音效 —— 不依赖任何音频资源。
/// 脚步、心跳、切换音、警报、被优化音 全部现场算出来。
/// </summary>
public static class AudioSynth
{
    const int SR = 44100;

    delegate float Gen(float t, System.Random rnd);

    static AudioClip Make(string name, float seconds, Gen gen)
    {
        int n = Mathf.Max(64, Mathf.RoundToInt(SR * seconds));
        var data = new float[n];
        var rnd = new System.Random(StableHash(name));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            data[i] = Mathf.Clamp(gen(t, rnd), -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }

    static float Noise(System.Random rnd) { return (float)rnd.NextDouble() * 2f - 1f; }

    // ── 老板的皮鞋声：低频肉感 + 高频皮面摩擦 ────────────────
    public static AudioClip Footstep()
    {
        return Make("footstep", 0.20f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 26f);
            float thump = Mathf.Sin(2f * Mathf.PI * 74f * t) * 0.75f;
            float slap = Noise(r) * Mathf.Exp(-t * 110f) * 0.55f;
            return (thump + slap) * env;
        });
    }

    // ── 心跳：危险临近 ──────────────────────────────────────
    public static AudioClip Heartbeat()
    {
        return Make("heartbeat", 1.0f, (t, r) =>
        {
            float b1 = Mathf.Exp(-t * 20f) * Mathf.Sin(2f * Mathf.PI * 52f * t);
            float t2 = Mathf.Max(0f, t - 0.24f);
            float gate = t > 0.24f ? 1f : 0f;
            float b2 = Mathf.Exp(-t2 * 24f) * Mathf.Sin(2f * Mathf.PI * 44f * t2) * gate * 0.8f;
            return (b1 + b2) * 0.85f;
        });
    }

    // ── 切回工作：清脆的咔哒 ────────────────────────────────
    public static AudioClip Click()
    {
        return Make("click", 0.09f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 70f);
            float body = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.5f;
            float n = Noise(r) * 0.5f;
            return (body + n) * env;
        });
    }

    // ── 摸鱼值上涨：气泡音 ──────────────────────────────────
    public static AudioClip Blip()
    {
        return Make("blip", 0.14f, (t, r) =>
        {
            float f = 520f + 900f * t;
            float env = Mathf.Exp(-t * 16f) * Mathf.Min(1f, t * 90f);
            return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
        });
    }

    // ── 老板盯人警报：下行的刺耳蜂鸣 ────────────────────────
    public static AudioClip Alert()
    {
        return Make("alert", 0.45f, (t, r) =>
        {
            float f = 980f - 420f * (t / 0.45f);
            float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
            float trem = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t);
            float env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 3.2f);
            return sq * trem * env * 0.32f;
        });
    }

    // ── 被抓：一记闷响 + 电话挂断 ──────────────────────────
    public static AudioClip Caught()
    {
        return Make("caught", 1.1f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 2.4f);
            float low = Mathf.Sin(2f * Mathf.PI * (170f - 60f * t) * t) * 0.7f;
            float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 62f * t)) * 0.18f;
            float n = Noise(r) * Mathf.Exp(-t * 9f) * 0.35f;
            return (low + buzz + n) * env * 0.85f;
        });
    }

    // ── 影帝加分：上行的小三度 ──────────────────────────────
    public static AudioClip Praise()
    {
        return Make("praise", 0.34f, (t, r) =>
        {
            int step = Mathf.Min(2, (int)(t / 0.11f));
            float[] f = { 660f, 880f, 1320f };
            float env = Mathf.Exp(-(t - step * 0.11f) * 15f);
            return Mathf.Sin(2f * Mathf.PI * f[step] * t) * env * 0.4f;
        });
    }

    // ── 虚惊一场：开门吱呀 + 一声闷响 ────────────────────────
    public static AudioClip DoorCreak()
    {
        return Make("creak", 0.75f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 3.4f) * Mathf.Min(1f, t * 22f);
            float f = 240f + 130f * Mathf.Sin(2f * Mathf.PI * 3.1f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * f * t + 3f * Mathf.Sin(t * 40f)) * 0.35f;
            float n = Noise(r) * 0.13f;
            return (tone + n) * env;
        });
    }

    // ── 通关：下班铃 + 小号角 ───────────────────────────────
    public static AudioClip Win()
    {
        return Make("win", 1.25f, (t, r) =>
        {
            float[] notes = { 523f, 659f, 784f, 1046f };
            int i = Mathf.Clamp((int)(t / 0.18f), 0, 3);
            float local = t - i * 0.18f;
            float env = Mathf.Exp(-local * 4.2f) * Mathf.Min(1f, local * 120f) * Mathf.Exp(-t * 1.1f);
            float tone = Mathf.Sin(2f * Mathf.PI * notes[i] * t) * 0.42f;
            float harm = Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * t) * 0.14f;
            return (tone + harm) * env;
        });
    }

    // ── 失败：下行长音 ──────────────────────────────────────
    public static AudioClip Lose()
    {
        return Make("lose", 1.6f, (t, r) =>
        {
            float[] notes = { 415f, 349f, 261f, 196f };
            int i = Mathf.Clamp((int)(t / 0.28f), 0, 3);
            float local = t - i * 0.28f;
            float env = Mathf.Exp(-local * 2.0f) * Mathf.Min(1f, local * 60f);
            float tone = Mathf.Sin(2f * Mathf.PI * notes[i] * t) * 0.4f;
            float n = Noise(r) * 0.05f;
            return (tone + n) * env;
        });
    }

    // ── UI：光标移动 / 打字机 ───────────────────────────────
    public static AudioClip Tick()
    {
        return Make("tick", 0.05f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 120f);
            return Mathf.Sin(2f * Mathf.PI * 2100f * t) * env * 0.3f;
        });
    }
}
