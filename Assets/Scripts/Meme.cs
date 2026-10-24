using UnityEngine;

/// <summary>
/// 全游戏的梗文案仓库。想加料就往数组里塞。
/// </summary>
public static class Meme
{
    // ── 摸鱼窗口：一局射击游戏 ────────────────────────────────
    public static readonly string[] ShooterTitles =
    {
        "反恐精英 · 办公室局域网",
        "穿越火线 · 摸鱼大区",
        "绝地求生 · 老板不在场",
        "使命召唤 · 带薪作战",
        "打枪.exe",
        "射击训练营 · 第 3 关",
        "CS · de_dust2（工位版）",
        "狙击精英 · 午休局",
    };

    /// <summary>击杀播报里的武器名</summary>
    public static readonly string[] ShooterWeapons =
    {
        "AK-47", "M4A1", "AWM", "沙鹰", "喷子", "平底锅", "小刀",
    };

    /// <summary>击杀播报里的"受害者"</summary>
    public static readonly string[] ShooterTargets =
    {
        "敌人_01", "敌人_02", "敌人_03", "靶子", "陪练机器人",
        "隔壁工位的同事(误)", "空气", "自己的影子",
    };

    /// <summary>打中时蹦出来的骚话</summary>
    public static readonly string[] ShooterTaunts =
    {
        "双杀！", "三杀！", "一枪一个", "这波稳了", "压枪压得不错",
        "子弹会拐弯", "他人呢？", "队友呢？", "别打了别打了",
        "手别抖", "这枪法可以啊", "老板在你后面（游戏里）",
    };

    // ── 工作窗口 ──────────────────────────────────────────────
    public static readonly string[] WorkTitles =
    {
        "2024年度Q4经营分析报告_final_v7_真的最终版.xlsx",
        "季度复盘_改完这版就下班.pptx",
        "需求文档_v12_勿删.docx",
        "KPI考核表（空）.xlsx",
        "周报_复制粘贴版.docx",
        "会议纪要_没人看.docx",
    };

    public static readonly string[] WorkBodies =
    {
        "正在努力工作……",
        "Ctrl+C   Ctrl+V\nCtrl+C   Ctrl+V",
        "数据加载中……\n（其实没在加载）",
        "正在假装努力工作……",
        "您已连续工作 0 分 12 秒\n请注意休息",
        "正在生成一份\n没人会看的报表",
        "把字号调大了 0.5\n显得内容更多",
        "正在等编译\n（编译是万能的借口）",
        "第 7 次修改\n「还是用第一版吧」",
    };

    // ── 老板台词 ──────────────────────────────────────────────
    public static readonly string[] BossLectures =
    {
        "小李啊，来我办公室一趟。",
        "你这个方案，今天能给我吗？",
        "年轻人，要多沉淀。",
        "这个需求很简单，你怎么还没做完？",
        "周末加个班，把进度追一下。",
        "我不是针对你，我是说在座的各位。",
        "公司现在很难，你要理解。",
        "我看好你哦。（微笑）",
        "你屏幕上这个，是什么？",
    };

    /// <summary>老板路过时的碎碎念（世界气泡，必须要短）</summary>
    public static readonly string[] BossMutter =
    {
        "嗯……", "业绩……", "优化……", "成本……", "降本增效……", "996……", "对齐一下……",
    };

    /// <summary>假动作虚晃时头顶的符号</summary>
    public static readonly string[] BossFeint =
    {
        "？", "嗯？", "……",
    };

    /// <summary>老板接电话（玩家的安全窗口）</summary>
    public static readonly string[] BossPhone =
    {
        "喂？王总啊……",
        "嗯嗯，好的好的。",
        "这个我们内部再评估一下。",
        "喂？信号不好……",
    };

    // ── 演出反馈 ──────────────────────────────────────────────
    public static readonly string[] Praise =
    {
        "影帝级演技！",
        "无缝切换，手速惊人",
        "你的手速超越了 99% 的打工人",
        "老板点了点头：这小伙子很忙。",
        "教科书级别的 Alt+Tab",
    };

    public static readonly string[] FakeAlarm =
    {
        "有人来了！",
        "……虚惊一场",
        "谁在拍我肩膀？",
        "（其实是空调）",
    };

    // ── 开始 / 结算 ───────────────────────────────────────────
    public const string Title = "老板来了";
    public const string Subtitle = "下班前的 75 秒，留一点时间给自己。";

    public static readonly string[] TitleRules =
    {
        "01   空格 / 回车 / 点击，在游戏与工作之间切换。",
        "02   留意老板的预警；被盯上时立刻切回工作。",
        "03   连摸收益更高，也会引起注意；75 秒内攒满 100。",
        "ESC   暂停休息    ·    你有 3 次被抓机会。",
    };

    public const string StartHint = "按任意键开始（趁老板还没出来）";

    public static readonly string[] WinLines =
    {
        "下班时间到。",
        "今日摸鱼值：100",
        "老板对你的评价：这小伙子很忙。",
        "你已连续 8 小时没有产生任何价值。",
    };

    public static readonly string[] LoseLines =
    {
        "HR：祝你前程似锦。（微笑）",
        "实习生已经在收拾你的工位了。",
        "你的工位将在 10 分钟后开放竞拍。",
    };

    public static string Grade(int slackScore)
    {
        if (slackScore >= 100) return "S · 摸鱼之神";
        if (slackScore >= 80) return "A · 摸鱼大师";
        if (slackScore >= 60) return "B · 熟练工";
        if (slackScore >= 40) return "C · 有待提高";
        if (slackScore >= 20) return "D · 你这人怎么这样";
        return "F · 你这种人居然还活着";
    }

    public static string Pick(string[] arr)
    {
        return arr[Random.Range(0, arr.Length)];
    }

    public static string PickNot(string[] arr, int avoidIdx)
    {
        if (arr.Length <= 1) return arr[0];
        int i;
        do { i = Random.Range(0, arr.Length); } while (i == avoidIdx);
        return arr[i];
    }
}
