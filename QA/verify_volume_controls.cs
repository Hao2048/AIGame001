var volume=UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(UnityEngine.FindObjectsSortMode.None).Single(v=>v.isGlobal);
var profile=volume.sharedProfile;
if(!profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var color))throw new System.Exception("Missing scene ColorAdjustments");
var camera=OfficeBuilder.Cam;
bool active=color.active,overridden=color.postExposure.overrideState;float original=color.postExposure.value;
var before=UnityEditor.EditorJsonUtility.ToJson(color);
var rt=new RenderTexture(320,180,24);rt.Create();var pixels=new Texture2D(320,180,TextureFormat.RGB24,false);
var target=camera.targetTexture;var previous=RenderTexture.active;
var means=new System.Collections.Generic.List<double>();
try{
 foreach(float exposure in new[]{0f,1.5f}){
  color.active=true;color.postExposure.overrideState=true;color.postExposure.value=exposure;
  camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,320,180),0,0);pixels.Apply();
  var colors=pixels.GetPixels32();double mean=0;foreach(var c in colors)mean+=(c.r*.2126+c.g*.7152+c.b*.0722)/255.0;means.Add(mean/colors.Length);
 }
}finally{
 color.active=active;color.postExposure.overrideState=overridden;color.postExposure.value=original;
 camera.targetTexture=target;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
 camera.Render();
}
var report=new {volume=volume.name,profile=UnityEditor.AssetDatabase.GetAssetPath(profile),volume.priority,baselineLuminance=means[0],raisedExposureLuminance=means[1],visibleResponse=means[1]>means[0]+.03,parametersRestored=before==UnityEditor.EditorJsonUtility.ToJson(color),postEnabled=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing};
var json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);
var folder=System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),"QA/volume-controls");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllText(System.IO.Path.Combine(folder,UnityEditor.EditorApplication.isPlaying?"runtime-validation.json":"editor-validation.json"),json);
return json;
