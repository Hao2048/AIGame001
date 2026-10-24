#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Reproducible public macOS player build from a clean checkout.</summary>
public static class MacReleaseBuilder
{
    public static void Build()
    {
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.dataPath));
        const string scene="Assets/Scenes/OfficeGame.unity";
        if(Directory.Exists("Assets/Resources/Realism/Renderpeople") || Directory.Exists("Assets/LocalOnly/Renderpeople"))
            throw new InvalidOperationException("Build public releases from a clean checkout without optional downloaded character data.");
        var dependencies=AssetDatabase.GetDependencies(scene,true);
        if(dependencies.Any(p=>p.Contains("Renderpeople")||p.Contains("LocalOnly")))
            throw new InvalidOperationException("The public scene has an optional-character dependency.");
        string[] args=Environment.GetCommandLineArgs();
        int index=Array.IndexOf(args,"-buildOutput");
        string output=index>=0 && index+1<args.Length ? args[index+1] : Path.GetFullPath("Builds/macOS/BossIsComing.app");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        PlayerSettings.companyName="Hao2048";PlayerSettings.productName="Boss Is Coming";PlayerSettings.bundleVersion="0.1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.hao2048.bossiscoming");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIcon.png");
        if(icon!=null)
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone,
                Enumerable.Repeat(icon,PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone,IconKind.Any).Length).ToArray(),IconKind.Any);
        EditorUserBuildSettings.SetPlatformSettings("Standalone","OSXUniversal","Architecture","x64ARM64");
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=true;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{scene},locationPathName=output,target=BuildTarget.StandaloneOSX,
            options=BuildOptions.CompressWithLz4HC|BuildOptions.DetailedBuildReport});
        var summary=report.summary;
        var result=new{result=summary.result.ToString(),output,version=PlayerSettings.bundleVersion,
            architecture=EditorUserBuildSettings.GetPlatformSettings("Standalone","OSXUniversal","Architecture"),
            bytes=summary.totalSize,errors=summary.totalErrors,warnings=summary.totalWarnings,
            seconds=summary.totalTime.TotalSeconds,optionalCharacterIncluded=false};
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-report.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("macOS build failed: "+summary.result);
        Debug.Log("Boss Is Coming macOS Universal release build succeeded.");
    }
}
#endif
