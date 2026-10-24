Shader "Moyu/Illustrated Toon"
{
 Properties
 {
  [MainTexture] _BaseMap("Color Map",2D)="white"{}
  [MainColor] _BaseColor("Color",Color)=(1,1,1,1)
  _ShadowColor("Cool Shadow",Color)=(0.68,0.72,0.90,1)
  _LightColor("Warm Light",Color)=(1.06,1.03,0.94,1)
  _RimColor("Edge Light",Color)=(0.72,0.84,1,1)
  [HDR] _EmissionColor("Emission",Color)=(0,0,0,0)
  [Normal] _BumpMap("Soft Fold Normal",2D)="bump"{}
  _BumpStrength("Fold Strength",Range(0,1))=0
  _HighlightStrength("Painted Highlight",Range(0,0.3))=0.06
  _TextureLod("Texture Detail Reduction",Range(0,6))=0
  _Character("Painted Character",Range(0,1))=0
  _RimStrength("Rim Strength",Range(0,1))=0.10
  _ShadowStrength("Cast Shadow Strength",Range(0,1))=0.72
  _BandSoftness("Band Softness",Range(0.01,0.3))=0.055
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;
   half4 _BaseColor, _ShadowColor, _LightColor, _RimColor, _EmissionColor;
   float _TextureLod, _Character, _RimStrength, _ShadowStrength, _BandSoftness, _Cull;
   float _BumpStrength, _HighlightStrength;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
  struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 tangentWS:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
  Varyings ToonVert(Attributes input)
  {
   Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
   o.positionCS=TransformWorldToHClip(o.positionWS);
   o.normalWS=TransformObjectToWorldNormal(input.normalOS);
   o.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
   o.uv=TRANSFORM_TEX(input.uv,_BaseMap); return o;
  }
  half4 ToonFrag(Varyings input):SV_Target
  {
   UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
   half3 sampled=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,input.uv,_TextureLod).rgb;
   if(_Character>.5)
   {
    // Keep the authored UVs, eyes and hair, while simplifying photographic skin and cloth detail.
    float2 uv=input.uv;
    half face=step(.60,uv.x)*(1-step(.36,uv.y));
    half hands=step(.49,uv.x)*(1-step(.68,uv.x))*step(.16,uv.y)*(1-step(.62,uv.y));
    half neck=(1-step(.26,uv.x))*step(.30,uv.y)*(1-step(.52,uv.y));
    half warm=step(sampled.g*1.12,sampled.r)*step(sampled.b*1.04,sampled.g);
    half skin=saturate(face+hands+neck)*warm*smoothstep(.018,.08,Luminance(sampled));
    half3 peach=half3(.90,.72,.57)*(0.95+smoothstep(.15,.7,Luminance(sampled))*.08);
    sampled=lerp(sampled,peach,skin*.26);
    // Lift black cloth into a readable ink blue, retaining buttons, tie and seams.
    half dark=1-smoothstep(.035,.18,Luminance(sampled));
    sampled=lerp(sampled,sampled*.82+half3(.018,.026,.040),dark*.62);
    sampled=lerp(sampled,(floor(sampled*24+.5)/24),.15);
   }
   half3 base=sampled*_BaseColor.rgb;
   half3 n=normalize(input.normalWS);
   if(_BumpStrength>.001)
   {
    half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D_LOD(_BumpMap,sampler_BumpMap,input.uv,2.0),_BumpStrength);
    half3 tangent=normalize(input.tangentWS.xyz);
    half3 bitangent=cross(n,tangent)*input.tangentWS.w;
    n=normalize(tangent*normalTS.x+bitangent*normalTS.y+n*normalTS.z);
   }
   Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
   half ndl=saturate(dot(n,light.direction)*.90+.10);
   half lower=smoothstep(.28-_BandSoftness,.28+_BandSoftness,ndl);
   half upper=smoothstep(.72-_BandSoftness,.72+_BandSoftness,ndl);
   half band=.32+lower*.33+upper*.40;
   half diffuse=lerp(.27+ndl*.85,band,.38);
   half castShadow=lerp(1,light.shadowAttenuation,_ShadowStrength);
   half3 tint=lerp(_ShadowColor.rgb,_LightColor.rgb,smoothstep(.12,.85,ndl));
   half fill=saturate(dot(n,normalize(half3(-.65,.38,.45))));
   half3 ambient=half3(.23,.255,.29)+half3(.07,.09,.13)*fill;
   AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
   half occlusion=lerp(1,ao.indirectAmbientOcclusion,.65);
   half3 key=light.color*.80;
   half3 color=base*(ambient*occlusion+diffuse*tint*key*castShadow)*lerp(1,occlusion,.20);
   half3 view=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
   half rim=pow(1-saturate(dot(n,view)),3)*_RimStrength;
   color+=_RimColor.rgb*rim*(.07+base*.15);
   half specular=pow(saturate(dot(n,SafeNormalize(light.direction+view))),32);
   color+=smoothstep(.18,.45,specular)*_HighlightStrength*castShadow*half3(1,.96,.86);
   // A subtle height fill keeps the room airy without a fog layer over the foreground.
   color*=lerp(.98,1.035,saturate(input.positionWS.y/3.5));
   return half4(color+_EmissionColor.rgb,1);
  }
  ENDHLSL
  Pass
  {
   Name "IllustratedForward" Tags {"LightMode"="UniversalForwardOnly"}
   Cull [_Cull] ZWrite On
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex ToonVert
   #pragma fragment ToonFrag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
   Cull [_Cull] ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection; float3 _LightPosition;
   float4 ShadowVert(Attributes input):SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(input);
    float3 p=TransformObjectToWorld(input.positionOS.xyz);
    float3 n=TransformObjectToWorldNormal(input.normalOS);
    float3 direction=_LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     direction=normalize(_LightPosition-p);
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
    #if UNITY_REVERSED_Z
     clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #else
     clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #endif
    return clip;
   }
   half4 ShadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   Cull [_Cull] ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex ToonVert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   half4 DepthFrag(Varyings input):SV_Target{return input.positionCS.z;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"}
   Cull [_Cull] ZWrite On
   HLSLPROGRAM
   #pragma vertex ToonVert
   #pragma fragment NormalsFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   half4 NormalsFrag(Varyings input):SV_Target
   {
    float3 n=normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
     return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
    #else
     return half4(n,0);
    #endif
   }
   ENDHLSL
  }
 }
}
