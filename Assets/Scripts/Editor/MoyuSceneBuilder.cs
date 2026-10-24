using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器菜单：把整个办公室直接生成到当前场景里并保存。
/// 这样打开工程 / 打开场景就能看见东西，不用先按 Play。
///
/// 按 Play 时 SlackGame 会用同一套代码重建一遍（结果完全一致），
/// 所以"编辑器里看到的"和"玩到的"永远是同一个东西。
/// </summary>
public static class MoyuSceneBuilder
{
    const string Menu = "Tools/老板来了/";

    [MenuItem(Menu + "① 生成场景（不按 Play 也能看见）", false, 0)]
    public static void BuildPreview()
    {
        // Keep the committed scene free of optional third-party character data, even on a local install.
        OfficeBuilder.BuildWorld(EditorSceneManager.GetActiveScene().path == "Assets/Scenes/OfficeGame.unity");

        var screen = Object.FindAnyObjectByType<ScreenDisplay>();
        if (screen != null) screen.EditorPreview();

        var hud = Object.FindAnyObjectByType<HudUI>();
        if (hud != null)
        {
            hud.ShowOverlay(false);
            hud.SetSlack(0.46f);
            hud.SetLives(2, 3);
            hud.SetPraise(3);
            hud.SetSession(49f, true, 1.3f, 0.5f, 0.35f, true);
            hud.SetHint("空格 / 点击  切换窗口   ·   ESC 暂停   ·   留意老板的预警");
            hud.Radar(1.75f, 1f, "正在盯着你！", true, new Color(1f, 0.22f, 0.18f));
        }

        var boss = Object.FindAnyObjectByType<BossController>();
        if (boss != null) boss.EditorPreview();

        SaveScene();
        Debug.Log("[老板来了] 场景已生成并保存。直接按 Play 就能玩；" +
                  "Scene 视图里选 PlayerCamera 可以看游戏视角。");
    }

    [MenuItem(Menu + "② 清空场景", false, 1)]
    public static void ClearScene()
    {
        var root = GameObject.Find("GameRoot");
        if (root != null) Object.DestroyImmediate(root);
        SaveScene();
        Debug.Log("[老板来了] 场景已清空（按 Play 仍会自动重新生成）。");
    }

    [MenuItem(Menu + "③ 进入播放模式", false, 20)]
    public static void Play()
    {
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    static void SaveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
