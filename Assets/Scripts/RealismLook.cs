using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Shared illustrated materials. The existing entry point is retained for model/scene compatibility.</summary>
public static class RealismLook
{
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    static Material Toon(Color color, string name)
    {
        var shader = Resources.Load<Shader>("Toon/OfficeToon");
        if (shader == null) throw new System.InvalidOperationException("Illustrated Toon shader is missing.");
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        return material;
    }

    public static Material Flat(Color color)
    {
        // Lift near-black geometry into a coherent ink palette; keep authored accent colors.
        float value = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (value < .16f) color = Color.Lerp(new Color(.075f,.12f,.19f), color, .30f);
        else if (Mathf.Abs(color.r-color.b) < .07f && value < .65f)
            color = Color.Lerp(color, new Color(.34f,.43f,.51f), .38f);
        return Toon(color, "Toon-OfficeColor");
    }

    public static Material Surface(string key)
    {
        if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
        Color color;
        switch(key)
        {
            case "Walnut": color = new Color(.75f,.54f,.33f); break;
            case "Carpet": color = new Color(.40f,.55f,.56f); break;
            case "Plaster": color = new Color(.94f,.95f,.91f); break;
            case "Suit": color = new Color(.12f,.19f,.28f); break;
            case "Shirt": color = new Color(.90f,.91f,.85f); break;
            case "Leather": color = new Color(.12f,.20f,.28f); break;
            case "Hair": color = new Color(.12f,.075f,.06f); break;
            case "Metal": color = new Color(.48f,.60f,.66f); break;
            case "Leaf": color = new Color(.25f,.57f,.36f); break;
            case "Soil": color = new Color(.14f,.12f,.15f); break;
            case "Ceramic": color = new Color(.94f,.82f,.58f); break;
            case "Coffee": color = new Color(.12f,.065f,.05f); break;
            case "Keycap": color = new Color(.20f,.29f,.37f); break;
            case "KeyLegend": color = new Color(.78f,.86f,.83f); break;
            default: color = new Color(.10f,.16f,.24f); break;
        }
        var m = Toon(color, "Toon-"+key);
        if (key == "Leaf") m.SetFloat("_Cull",0f);
        if (key == "Carpet" || key == "Plaster") m.SetFloat("_RimStrength",0f);
        if (key == "Plaster") m.SetColor("_EmissionColor",new Color(.055f,.058f,.055f));
        Materials[key] = m;
        return m;
    }

    public static Material Glass()
    {
        const string key = "IllustratedGlass";
        if (Materials.TryGetValue(key,out var cached) && cached != null) return cached;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = key };
        m.SetColor("_BaseColor",new Color(.65f,.85f,.90f,.06f));
        m.SetFloat("_Surface",1f); m.SetFloat("_Blend",0f);
        m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite",0f);m.SetFloat("_Cull",0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        Materials[key]=m; return m;
    }

    static Material RenderpeopleSkin(bool blackBack = false)
    {
        string key=blackBack ? "Toon-EricBlackBack" : "Toon-Eric";
        if(Materials.TryGetValue(key,out var cached)&&cached!=null)return cached;
        var m=Toon(blackBack ? new Color(.10f,.14f,.20f,1f) : Color.white,key);
        m.SetTexture("_BaseMap",Resources.Load<Texture2D>("Realism/Renderpeople/tex/rp_eric_rigged_001_dif"));
        m.SetFloat("_Character",1f);m.SetFloat("_TextureLod",2f);
        m.SetTexture("_BumpMap",Resources.Load<Texture2D>("Realism/Renderpeople/tex/rp_eric_rigged_001_norm"));
        m.SetFloat("_BumpStrength",.18f);m.SetFloat("_HighlightStrength",.025f);
        m.SetFloat("_RimStrength",.11f);m.SetFloat("_ShadowStrength",.55f);
        m.SetFloat("_BandSoftness",.04f);
        Materials[key]=m; return m;
    }

    static void AddContour(Renderer source)
    {
        const string key = "SoftInk";
        if (!Materials.TryGetValue(key, out var ink) || ink == null)
        {
            ink = new Material(Resources.Load<Shader>("Toon/Contour")) { name = "Soft Ink Contour" };
            ink.SetColor("_Color", new Color(.045f,.065f,.10f));
            ink.SetFloat("_Width",.80f); Materials[key] = ink;
        }
        var go = new GameObject("InkContour");go.transform.SetParent(source.transform,false);
        Renderer outline;
        if (source is SkinnedMeshRenderer skin)
        {
            var copy = go.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh=skin.sharedMesh;copy.bones=skin.bones;copy.rootBone=skin.rootBone;
            copy.localBounds=skin.localBounds;copy.updateWhenOffscreen=true;copy.quality=skin.quality;
            var mats=new Material[skin.sharedMesh.subMeshCount];for(int i=0;i<mats.Length;i++)mats[i]=ink;
            copy.sharedMaterials=mats;outline=copy;
        }
        else
        {
            var filter=source.GetComponent<MeshFilter>();
            if (filter == null) { Object.DestroyImmediate(go); return; }
            go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var copy=go.AddComponent<MeshRenderer>();
            var mats=new Material[filter.sharedMesh.subMeshCount];for(int i=0;i<mats.Length;i++)mats[i]=ink;
            copy.sharedMaterials=mats;outline=copy;
        }
        outline.shadowCastingMode=ShadowCastingMode.Off;outline.receiveShadows=false;
        outline.lightProbeUsage=LightProbeUsage.Off;outline.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }

    public static void GroundContact(Transform boss)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="ToonGroundContact";
        go.transform.SetParent(boss,false);go.transform.localScale=new Vector3(.70f,.48f,1f);
        var collider=go.GetComponent<Collider>();
        if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
        const string key="PaintedGroundContact";
        if(!Materials.TryGetValue(key,out var material)||material==null)
        {
            material=new Material(Resources.Load<Shader>("Toon/GroundContact")) {name=key};Materials[key]=material;
        }
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    public static GameObject Model(string name, Transform parent, Vector3 position, float scale = 1f)
    {
        var asset = Resources.Load<GameObject>("Realism/"+name);
        if (asset == null) return null;
        var model = Object.Instantiate(asset,parent,false);
        model.name = name;
        model.transform.localPosition = position;
        model.transform.localScale = Vector3.one * scale;
        foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (name == "Renderpeople/Eric" && r is SkinnedMeshRenderer eric)
            {
                var mesh = Resources.Load<Mesh>("Realism/Renderpeople/EricBlackBack");
                if (mesh != null) eric.sharedMesh = mesh;
            }
            var mats = r.sharedMaterials;
            if (name == "Renderpeople/Eric" && r is SkinnedMeshRenderer vest && vest.sharedMesh.subMeshCount == 2)
                mats = new Material[2];
            for (int i = 0; i < mats.Length; i++)
            {
                string key = mats[i] != null ? mats[i].name.Replace("MAT-","").Replace(" (Instance)","") : "Plastic";
                mats[i] = name.StartsWith("Renderpeople/") ? RenderpeopleSkin(i == 1) : Surface(key);
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            if (r is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen = true; skin.quality = SkinQuality.Bone4; }
            if (name.StartsWith("Renderpeople/") || name == "OfficeMonitor") AddContour(r);
        }
        return model;
    }

    public static void Configure(Camera camera, Transform parent)
    {
        var data=camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality=AntialiasingQuality.High;data.volumeLayerMask=1;
        EnsureEditableVolume(parent);
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.70f,.81f,.94f);
        RenderSettings.ambientEquatorColor=new Color(.62f,.70f,.79f);
        RenderSettings.ambientGroundColor=new Color(.39f,.43f,.53f);
        RenderSettings.reflectionIntensity=0f;
        // The illustrated interior receives sunlight and prop shadows without a closed shell occluding them.
        var room=parent.Find("Room");
        if(room!=null)foreach(var renderer in room.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
        foreach(var light in parent.GetComponentsInChildren<Light>())
        {
            if(light.name!="Sun")light.enabled=false;
        }
    }

    static void EnsureEditableVolume(Transform generatedRoot)
    {
        // Scene-authored post processing is the source of truth, including its enabled/override states.
        // Keep it outside GameRoot so rebuilding the office never replaces Inspector edits.
        foreach(var existing in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(existing.isGlobal && !existing.transform.IsChildOf(generatedRoot)) return;

        // Only a scene with no Global Volume receives defaults. These are not reapplied on Play.
        var go=new GameObject("Global Volume");
        var volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=0f;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Illustrated Office Grade";
        var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=.12f;color.contrast.value=7f;color.saturation.value=6f;
        var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.4f;bloom.intensity.value=.065f;bloom.scatter.value=.5f;
        var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.025f;vignette.smoothness.value=.45f;
        vignette.color.value=new Color(.12f,.17f,.26f);
        profile.Add<MotionBlur>(true).intensity.value=0f;
        profile.Add<DepthOfField>(true).mode.value=DepthOfFieldMode.Off;
        volume.sharedProfile=profile;
    }
}
