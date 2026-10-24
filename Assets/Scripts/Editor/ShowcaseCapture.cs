#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Offline capture of curated real gameplay. No changes to gameplay scores, lives or rules.</summary>
[InitializeOnLoad]
public static class ShowcaseCapture
{
    const int Width=1600, Height=900, Fps=30;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static System.Diagnostics.Process encoder;
    static BinaryWriter audio;
    static RenderTexture target;
    static Texture2D pixels;
    static byte[] rgb;
    static Camera portrait;
    static Coroutine director;
    static bool running,finished;
    static int frame,lastPlayerFrame,oldCapture,oldTargetRate,channels,sampleRate;
    static long pcmBytes;
    static float startTime;
    static string folder;
    static string[] oldLectures,oldTitles,oldBodies;
    static readonly List<object> events=new List<object>();
    static BossController.BState previousState;

    static ShowcaseCapture()
    {
        AssemblyReloadEvents.beforeAssemblyReload+=()=>Stop("assembly reload");
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingPlayMode)Stop("Play stopped");};
    }

    [MenuItem("Tools/老板来了/④ 录制搞笑演示（Play 模式）",false,40)]
    public static void Begin()
    {
        if(!EditorApplication.isPlaying||SlackGame.I==null)throw new InvalidOperationException("Enter Play mode first.");
        if(running)throw new InvalidOperationException("A showcase capture is already running.");
        folder=Path.Combine(Directory.GetCurrentDirectory(),"Showcase/raw");Directory.CreateDirectory(folder);
        File.Delete(Path.Combine(folder,"complete.json"));events.Clear();frame=0;pcmBytes=0;lastPlayerFrame=-1;finished=false;
        oldCapture=Time.captureFramerate;oldTargetRate=Application.targetFrameRate;
        Time.timeScale=1f;AudioListener.pause=false;Time.captureFramerate=Fps;Application.targetFrameRate=Fps;
        OfficeBuilder.Cam.aspect=Width/(float)Height;
        sampleRate=AudioSettings.outputSampleRate;
        channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:AudioSettings.speakerMode==AudioSpeakerMode.Stereo?2:0;
        if(channels==0)throw new InvalidOperationException("Capture expects mono or stereo audio.");
        if(!AudioRenderer.Start())throw new InvalidOperationException("Could not start offline audio rendering.");
        encoder=new System.Diagnostics.Process();encoder.StartInfo=new System.Diagnostics.ProcessStartInfo{
            FileName=Environment.GetEnvironmentVariable("FFMPEG_PATH") ??
                (File.Exists("/opt/homebrew/bin/ffmpeg") ? "/opt/homebrew/bin/ffmpeg" : "ffmpeg"),
            Arguments="-y -loglevel error -f rawvideo -pixel_format rgb24 -video_size 1600x900 -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+Path.Combine(folder,"gameplay.mp4")+"\"",
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true};
        encoder.Start();
        audio=new BinaryWriter(File.Open(Path.Combine(folder,"gameplay.wav"),FileMode.Create));WriteWaveHeader(0);
        target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);target.Create();
        pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);rgb=new byte[Width*Height*3];
        var go=new GameObject("ShowcasePortrait",typeof(Camera));portrait=go.GetComponent<Camera>();portrait.enabled=false;
        portrait.fieldOfView=40f;portrait.nearClipPlane=.03f;portrait.allowHDR=true;
        portrait.clearFlags=CameraClearFlags.SolidColor;portrait.backgroundColor=new Color(.5f,.7f,.8f);
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
        oldLectures=(string[])Meme.BossLectures.Clone();oldTitles=(string[])Meme.WorkTitles.Clone();oldBodies=(string[])Meme.WorkBodies.Clone();
        for(int i=0;i<Meme.BossLectures.Length;i++)Meme.BossLectures[i]="你屏幕上这个，是什么？";
        for(int i=0;i<Meme.WorkTitles.Length;i++)Meme.WorkTitles[i]=oldTitles[0];
        for(int i=0;i<Meme.WorkBodies.Length;i++)Meme.WorkBodies[i]=oldBodies[3];
        running=true;startTime=Time.time;previousState=BossController.BState.Hidden;
        director=SlackGame.I.StartCoroutine(Direct());EditorApplication.update+=Tick;
        Debug.Log("Showcase recording started: real frames + offline mixed game audio.");
    }

    static void Mark(string name){events.Add(new{name,frame,time=frame/(float)Fps});}
    static IEnumerator BossRoutine(string name,params object[] args)
    {
        return (IEnumerator)typeof(BossController).GetMethod(name,Flags).Invoke(OfficeBuilder.Boss,args);
    }
    static void Toggle(){typeof(SlackGame).GetMethod("DoToggle",Flags).Invoke(SlackGame.I,null);}
    static IEnumerator Direct()
    {
        UnityEngine.Random.InitState(194);
        var game=SlackGame.I;var boss=OfficeBuilder.Boss;
        typeof(SlackGame).GetMethod("StartRound",Flags).Invoke(game,null);boss.Halt();
        Mark("shooting");yield return new WaitForSeconds(2.5f);
        boss.transform.position=new Vector3(2.8f,0f,OfficeBuilder.CorridorZ);boss.transform.rotation=Quaternion.LookRotation(Vector3.left);
        boss.SetVisible(true);game.OnBossEnter();Mark("boss_walk");
        yield return boss.StartCoroutine(BossRoutine("WalkTo",1.85f,1.65f));
        Mark("phone");yield return boss.StartCoroutine(BossRoutine("PhoneCall"));yield return new WaitForSeconds(.5f);
        yield return boss.StartCoroutine(BossRoutine("WalkTo",1.25f,1.45f));
        Mark("save_warn");bool done=false;
        var look=boss.StartCoroutine(FinishRoutine(BossRoutine("LookSequence"),()=>done=true));
        while(boss.State!=BossController.BState.Watch)yield return null;
        yield return new WaitForSeconds(.10f);Toggle();Mark("switch_to_work");
        while(!done)yield return null;
        Mark("work");yield return new WaitForSeconds(1.15f);
        Mark("back_to_shooting");Toggle();
        yield return boss.StartCoroutine(BossRoutine("WalkTo",2.65f,1.65f));yield return new WaitForSeconds(1.3f);
        Mark("greed");yield return boss.StartCoroutine(BossRoutine("WalkTo",1.65f,1.65f));
        Mark("caught_warn");yield return boss.StartCoroutine(BossRoutine("LookSequence"));
        Mark("back_again");yield return new WaitForSeconds(2.8f);
        Mark("end");finished=true;
    }
    static IEnumerator FinishRoutine(IEnumerator routine,Action complete){yield return routine;complete();}

    static void Tick()
    {
        if(!running)return;
        if(!EditorApplication.isPlaying||SlackGame.I==null){Stop("Play stopped");return;}
        if(Time.frameCount==lastPlayerFrame)return;lastPlayerFrame=Time.frameCount;
        if(finished||frame>Fps*50){Stop(finished?"complete":"timeout");return;}
        try
        {
            var boss=OfficeBuilder.Boss;
            if(boss.State!=previousState){Mark("state_"+boss.State);previousState=boss.State;}
            Camera camera=OfficeBuilder.Cam;
            if(boss.State==BossController.BState.Lecture)
            {
                var root=boss.transform;
                portrait.transform.position=root.position+root.forward*.95f+root.right*.10f+Vector3.up*1.95f;
                portrait.transform.LookAt(root.position+Vector3.up*1.65f);camera=portrait;
            }
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();
                pixels.GetRawTextureData<byte>().CopyTo(rgb);
                encoder.StandardInput.BaseStream.Write(rgb,0,rgb.Length);
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;}
            int count=AudioRenderer.GetSampleCountForCaptureFrame();
            using(var buffer=new NativeArray<float>(count*channels,Allocator.Temp))
            {
                if(!AudioRenderer.Render(buffer))throw new InvalidOperationException("Offline audio capture stopped.");
                foreach(float sample in buffer){audio.Write((short)(Mathf.Clamp(sample,-1f,1f)*32767));pcmBytes+=2;}
            }
            frame++;
            if(frame%30==0)File.WriteAllText(Path.Combine(folder,"progress.json"),JsonUtility.ToJson(new Progress{frames=frame,seconds=frame/(float)Fps,state=boss.State.ToString()}));
        }
        catch(Exception e){Debug.LogException(e);Stop("error: "+e.Message);}
    }
    [Serializable] class Progress{public int frames;public float seconds;public string state;}
    static void WriteWaveHeader(int length)
    {
        audio.Seek(0,SeekOrigin.Begin);audio.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));audio.Write(36+length);
        audio.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));audio.Write(16);audio.Write((short)1);audio.Write((short)channels);
        audio.Write(sampleRate);audio.Write(sampleRate*channels*2);audio.Write((short)(channels*2));audio.Write((short)16);
        audio.Write(System.Text.Encoding.ASCII.GetBytes("data"));audio.Write(length);
    }
    [MenuItem("Tools/老板来了/停止演示录制",false,41)]
    public static void StopMenu(){Stop("stopped");}
    static void Stop(string reason)
    {
        if(!running)return;running=false;EditorApplication.update-=Tick;
        AudioRenderer.Stop();Time.captureFramerate=oldCapture;Time.timeScale=1f;Application.targetFrameRate=oldTargetRate;AudioListener.pause=false;
        if(SlackGame.I!=null){if(director!=null)SlackGame.I.StopCoroutine(director);OfficeBuilder.Boss.StopAllCoroutines();OfficeBuilder.Boss.Halt();}
        if(OfficeBuilder.Cam!=null)OfficeBuilder.Cam.ResetAspect();
        if(oldLectures!=null)Array.Copy(oldLectures,Meme.BossLectures,oldLectures.Length);
        if(oldTitles!=null)Array.Copy(oldTitles,Meme.WorkTitles,oldTitles.Length);
        if(oldBodies!=null)Array.Copy(oldBodies,Meme.WorkBodies,oldBodies.Length);
        if(audio!=null){WriteWaveHeader((int)pcmBytes);audio.Dispose();audio=null;}
        string encoderError="";int exit=-1;
        if(encoder!=null){encoder.StandardInput.Close();encoder.WaitForExit(10000);encoderError=encoder.StandardError.ReadToEnd();exit=encoder.ExitCode;encoder.Dispose();encoder=null;}
        if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
        if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
        if(portrait!=null)UnityEngine.Object.DestroyImmediate(portrait.gameObject);
        var result=new{reason,frames=frame,duration=frame/(float)Fps,width=Width,height=Height,fps=Fps,audioFrames=pcmBytes/(2*channels),sampleRate,channels,encoderExit=exit,encoderError,events};
        File.WriteAllText(Path.Combine(folder,"complete.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("Showcase capture finished: "+reason+", "+frame+" frames.");
    }
}
#endif
