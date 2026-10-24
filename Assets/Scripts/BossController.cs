using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 老板 AI：假装路过 → 虚晃一枪 → 突然回头盯人 → 抓到你摸鱼就过来训话。
/// 用协程写状态机，方便加"节目效果"。
/// </summary>
public class BossController : MonoBehaviour
{
    public SlackGame game;

    public enum BState { Hidden, Patrol, Feint, PhoneCall, Warn, Watch, Approach, Lecture, Leave }

    public BState State { get; private set; } = BState.Hidden;
    public float Danger { get; private set; }          // 0~1，给 HUD 用
    public bool Visible => State != BState.Hidden;
    public float X => transform.position.x;
    public float Exposure => State == BState.Watch ? Mathf.Clamp01(_slackSeen / 0.38f) : 0f;

    const float EntryX = 4.7f;
    const float WalkSpeed = 1.65f;
    const float PatrolLimit = 3.15f;

    Transform _body, _bubblePivot, _alertPivot;
    Text _bubble, _alert;
    Camera _cam;

    float _bobT, _stepPhase, _bubbleTimer;
    bool _walking;
    public bool IsWalking => _walking;
    CharacterMotion _motion;
    Coroutine _loop;
    float _watchElapsed, _slackSeen;
    bool _wasSlackingAtWarn, _wasSlackingAtWatch, _praised;
    bool _caught;
    float _yawVelocity, _moveSpeed;
    public float FacingError { get; private set; }
    Vector3 _alertScale;

    public void Init(SlackGame g, Transform body, Transform bubblePivot, Text bubble,
                     Transform alertPivot, Text alert, Camera cam)
    {
        game = g;
        _body = body;
        _motion = body != null ? body.GetComponent<CharacterMotion>() : null;
        _bubblePivot = bubblePivot;
        _bubble = bubble;
        _alertPivot = alertPivot;
        _alertScale = alertPivot != null ? alertPivot.localScale : Vector3.one;
        _alert = alert;
        _cam = cam;
        SetVisible(false);
    }

    public void SetVisible(bool v)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        if (_bubblePivot != null) _bubblePivot.gameObject.SetActive(v);
        if (_alertPivot != null) _alertPivot.gameObject.SetActive(v);
    }

    /// <summary>
    /// 编辑器里摆一个"他正在盯着你"的姿势，这样不按 Play 也能在 Scene 视图看到老板。
    /// 摆完的 transform / Renderer.enabled / 文字都会被存进场景。
    /// </summary>
    public void EditorPreview()
    {
        State = BState.Watch;
        Danger = 1f;
        _walking = false;
        transform.localPosition = new Vector3(1.75f, 0f, OfficeBuilder.CorridorZ);
        transform.localRotation = Quaternion.LookRotation(
            new Vector3(0f, 1.2f, -1f) - transform.position, Vector3.up);
        SetVisible(true);
        if (_motion != null) _motion.Preview(BState.Watch);
        if (_body != null)
        {
            _body.localPosition = Vector3.zero;
            _body.localRotation = Quaternion.identity;
        }
        if (_bubble != null)
        {
            _bubble.text = "";
            _bubble.color = new Color(0.85f, 0.85f, 0.90f);
        }
        if (_alert != null)
        {
            _alert.gameObject.SetActive(true);
            _alert.text = "！";
            _alert.color = new Color(1f, 0.25f, 0.20f);
        }
    }

    public void Begin()
    {
        if (_loop != null) StopCoroutine(_loop);
        _caught = _praised = false;
        _watchElapsed = _slackSeen = _bubbleTimer = 0f;
        _bobT = _stepPhase = 0f;
        _moveSpeed = _yawVelocity = 0f;
        FacingError = 0f;
        if (_bubble != null) _bubble.text = "";
        SetAlert(false);
        _loop = StartCoroutine(Run());
    }

    public void Halt()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        State = BState.Hidden;
        _moveSpeed = _yawVelocity = 0f;
        FacingError = 0f;
        Danger = 0f;
        _walking = false;
        SetAlert(false);
        _slackSeen = 0f;
        SetVisible(false);
    }

    // ── 主循环 ───────────────────────────────────────────────
    IEnumerator Run()
    {
        while (true)
        {
            // 1) 在办公室里待着（偶尔来个门响吓你一下）
            State = BState.Hidden;
            Danger = 0f;
            SetVisible(false);
            transform.position = new Vector3(3.4f, 0f, 7.4f);
            float wait = Random.Range(1.5f, 3.2f) * Mathf.Lerp(1f, 0.55f, game.Difficulty);
            float t = 0f;
            bool fakeOut = Random.value < 0.34f;
            float fakeAt = Random.Range(0.4f, wait * 0.8f);
            bool fired = false;
            while (t < wait)
            {
                t += Time.deltaTime;
                if (fakeOut && !fired && t >= fakeAt)
                {
                    fired = true;
                    game.OnFakeAlarm();
                }
                yield return null;
            }

            // 2) 出场
            int side = Random.value < 0.5f ? -1 : 1;
            transform.position = new Vector3(EntryX * side, 0f, OfficeBuilder.CorridorZ);
            transform.rotation = Quaternion.LookRotation(Vector3.left * side);
            _moveSpeed = _yawVelocity = 0f;
            SetVisible(true);
            _walking = true;
            game.OnBossEnter();

            float targetX = -side * Random.Range(0.6f, PatrolLimit);
            yield return WalkTo(targetX, WalkSpeed);

            // 3) 巡逻 / 虚晃 / 盯人
            int segments = Random.Range(2, game.Difficulty > 0.65f ? 3 : 4);
            int count = 0;
            int looks = 0;
            while (true)
            {
                count++;
                if (count >= segments || game.Flow >= 5f)
                {
                    count = 0;
                    segments = Random.Range(2, game.Difficulty > 0.65f ? 3 : 4);
                    yield return LookSequence();
                    if (_caught) { _caught = false; break; }   // 被抓了，回到"在办公室"重来
                    looks++;
                    if (looks >= 2)
                    {
                        yield return WalkTo(EntryX * side, WalkSpeed * 1.15f);
                        break;
                    }
                    // 盯完人继续溜达
                    targetX = Random.Range(-PatrolLimit, PatrolLimit);
                    yield return WalkTo(targetX, WalkSpeed * Random.Range(0.85f, 1.15f));
                    continue;
                }

                float r = Random.value;
                if (r < 0.26f && game.Flow < 3f)
                {
                    yield return PhoneCall();
                    // 接完电话先走两步，不搞"挂电话立刻回头"的偷袭
                    targetX = Random.Range(-PatrolLimit, PatrolLimit);
                    yield return WalkTo(targetX, WalkSpeed * 0.8f);
                }
                else if (r < 0.62f) yield return Feint();
                else
                {
                    targetX = Random.Range(-PatrolLimit, PatrolLimit);
                    yield return WalkTo(targetX, WalkSpeed * Random.Range(0.85f, 1.2f));
                }
            }
        }
    }

    // ── 状态实现 ─────────────────────────────────────────────

    IEnumerator WalkTo(float targetX, float speed)
    {
        State = BState.Patrol;
        Danger = 0.12f;
        _walking = true;
        while (Mathf.Abs(transform.position.x - targetX) > 0.06f)
        {
            // A long uninterrupted game session attracts attention. Never interrupt an exit.
            if (game.Flow >= 5f && Mathf.Abs(targetX) <= PatrolLimit) break;
            MoveTowards(new Vector3(targetX, 0f, OfficeBuilder.CorridorZ), speed);
            yield return null;
        }
        _walking = false;
        _moveSpeed = 0f;
    }

    IEnumerator Feint()
    {
        State = BState.Feint;
        _walking = false;
        Danger = 0.35f;
        ShowBubble(Meme.Pick(Meme.BossFeint), new Color(0.95f, 0.75f, 0.2f), 0.7f);
        float t = Random.Range(0.35f, 0.75f);
        while (t > 0f) { t -= Time.deltaTime; yield return null; }
        // 虚晃一枪，转身继续走
    }

    /// <summary>老板接电话：一段完全安全的窗口，让玩家敢放心摸。</summary>
    IEnumerator PhoneCall()
    {
        State = BState.PhoneCall;
        _walking = false;
        Danger = 0.04f;
        ShowBubble(Meme.Pick(Meme.BossPhone), new Color(0.35f, 0.85f, 0.5f), 2.6f);
        game.OnBossPhone();
        float t = Random.Range(1.8f, 3.2f);
        while (t > 0f) { t -= Time.deltaTime; yield return null; }
    }

    IEnumerator LookSequence()
    {
        // 预警：停下、头顶冒问号
        State = BState.Warn;
        _walking = false;
        Danger = 0.55f;
        _wasSlackingAtWarn = game.IsSlacking;
        ShowBubble("？", new Color(1f, 0.85f, 0.25f), 1.2f);
        float warn = Mathf.Lerp(0.80f, 0.52f, game.Difficulty);
        while (warn > 0f) { warn -= Time.deltaTime; yield return null; }

        // 回头盯人
        State = BState.Watch;
        _watchElapsed = 0f;
        _slackSeen = 0f;
        _praised = false;
        _wasSlackingAtWatch = game.IsSlacking;
        SetAlert(true);
        game.OnBossWatchStart();

        // 刚出预警就切回工作 = 影帝
        if (_wasSlackingAtWarn && !game.IsSlacking) { AwardPraise(); }

        float watch = Mathf.Lerp(0.95f, 1.9f, game.Difficulty) + Random.Range(-0.12f, 0.28f);
        while (_watchElapsed < watch)
        {
            _watchElapsed += Time.deltaTime;
            Danger = 1f;
            FaceTowards(new Vector3(0f, 1.1f, -1.0f));

            if (!_praised && _watchElapsed < 0.42f && _wasSlackingAtWatch && !game.IsSlacking)
            {
                AwardPraise();
            }

            if (game.IsSlacking)
            {
                _slackSeen += Time.deltaTime;
                if (_slackSeen > 0.38f)
                {
                    SetAlert(false);
                    yield return CatchSequence();
                    yield break;
                }
            }
            else _slackSeen = 0f;

            yield return null;
        }

        SetAlert(false);
        Danger = 0.25f;
        game.OnBossWatchEnd();
    }

    void AwardPraise()
    {
        _praised = true;
        game.AwardPraise();
    }

    IEnumerator CatchSequence()
    {
        _caught = true;
        State = BState.Approach;
        _walking = true;
        game.OnCaught();
        ShowBubble("抓到你了", new Color(1f, 0.3f, 0.25f), 2.0f);

        var target = new Vector3(0.05f, 0f, 0.45f);
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            MoveTowards(target, 2.1f);
            yield return null;
        }
        _walking = false;

        State = BState.Lecture;
        Danger = 0.6f;
        ShowBubble("", Color.white, 0f);
        game.OnLecture(Meme.Pick(Meme.BossLectures));
        _moveSpeed = 0f;
        float t = 1.6f;
        while (t > 0f) { FaceTowards(new Vector3(0f, 0f, -1f)); t -= Time.deltaTime; yield return null; }

        State = BState.Leave;
        Danger = 0f;
        _walking = true;
        var exit = new Vector3(Random.value < 0.5f ? -7f : 7f, 0f, OfficeBuilder.CorridorZ + 0.4f);
        while (Vector3.Distance(transform.position, exit) > 0.15f)
        {
            MoveTowards(exit, 2.4f);
            yield return null;
        }
        _walking = false;
        game.OnBossLeave();
        SetVisible(false);
    }

    // ── 表现 ─────────────────────────────────────────────────

    void MoveTowards(Vector3 target, float speed)
    {
        var delta = target - transform.position;
        delta.y = 0f;
        FaceTowards(target);
        // Turn before committing weight to a new direction; brake before the destination.
        float alignment = Mathf.InverseLerp(.15f, .95f, Vector3.Dot(transform.forward, delta.normalized));
        float desired = Mathf.Min(speed, Mathf.Sqrt(2f * 4.5f * delta.magnitude)) * alignment;
        _moveSpeed = Mathf.MoveTowards(_moveSpeed, desired, 4.5f * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, _moveSpeed * Time.deltaTime);
    }

    void FaceTowards(Vector3 target)
    {
        if (Time.deltaTime <= 0f) return;
        var dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-5f) return;
        float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, want, ref _yawVelocity,
            .20f, 300f, Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        FacingError = Mathf.DeltaAngle(yaw, want);
    }

    void ShowBubble(string text, Color color, float seconds)
    {
        if (_bubble == null) return;
        _bubble.text = text;
        _bubble.color = color;
        _bubbleTimer = seconds;
    }

    void SetAlert(bool on)
    {
        if (_alert == null) return;
        _alert.gameObject.SetActive(on);
        if (on && _bubble != null) _bubble.text = "";
    }

    void LateUpdate()
    {
        // 走路的时候抖起来（很滑稽）
        if (_body != null)
        {
            if (_walking)
            {
                _bobT += Time.deltaTime * 8.2f;
                float up = Mathf.Abs(Mathf.Sin(_bobT)) * 0.055f;
                if (_motion == null) {
                    _body.localPosition = new Vector3(0f, up, 0f);
                    _body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_bobT) * 5.5f);
                }

                float s = Mathf.Sin(_bobT);
                if (_stepPhase <= 0f && s > 0f)
                {
                    _stepPhase = 1f;
                    float vol = Mathf.Clamp01(1.4f - Vector3.Distance(transform.position,
                        _cam != null ? _cam.transform.position : Vector3.zero) / 6f);
                    if (_motion == null) game.PlayFootstep(Mathf.Clamp01(vol) * 0.85f);
                }
                else if (s <= 0f) _stepPhase = 0f;
            }
            else
            {
                _bobT = 0f;
                if (_motion == null) {
                _body.localPosition = Vector3.Lerp(_body.localPosition, Vector3.zero, 1f - Mathf.Exp(-8f * Time.deltaTime));
                _body.localRotation = Quaternion.Slerp(_body.localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
            }
        }

        // 气泡计时
        if (_bubbleTimer > 0f)
        {
            _bubbleTimer -= Time.deltaTime;
            if (_bubbleTimer <= 0f && _bubble != null) _bubble.text = "";
        }

        // 盯人时警报闪
        if (_alert != null && _alert.gameObject.activeSelf)
        {
            float k = 0.65f + 0.35f * Mathf.Sin(Time.time * 16f);
            _alert.color = new Color(1f, 0.2f * k, 0.18f * k, 1f);
            float s = 1f + 0.16f * Mathf.Sin(Time.time * 16f);
            _alertPivot.localScale = _alertScale * s;
        }

        // 雷达状态文字
        if (_bubble != null && State == BState.Patrol && _bubbleTimer <= 0f && Random.value < Time.deltaTime * 0.35f)
            ShowBubble(Meme.Pick(Meme.BossMutter), new Color(0.35f, 0.35f, 0.40f), 1.4f);
    }

    public string StateLabel()
    {
        switch (State)
        {
            case BState.Hidden: return "办公室（暂时安全）";
            case BState.Patrol: return "在溜达";
            case BState.Feint: return "停下来了…";
            case BState.PhoneCall: return "在接电话（安全）";
            case BState.Warn: return "好像要回头！";
            case BState.Watch: return "正在盯着你！";
            case BState.Approach: return "朝你走过来了";
            case BState.Lecture: return "正在训话";
            case BState.Leave: return "走了";
        }
        return "";
    }
}
