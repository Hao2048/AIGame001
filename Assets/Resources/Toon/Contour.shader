Shader "Moyu/Soft Ink Contour"
{
 Properties { _Color("Ink",Color)=(.06,.08,.13,1) _Width("Pixels",Range(0,4))=1.1 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"}
   Cull Front ZWrite On ZTest LEqual
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color; float _Width;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   float4 Vert(A i):SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(i);
    float4 p=TransformObjectToHClip(i.positionOS.xyz);
    float3 n=TransformWorldToViewDir(TransformObjectToWorldNormal(i.normalOS));
    float2 direction=n.xy/max(length(n.xy),.001);
    p.xy+=direction*(_Width*2/_ScreenParams.xy)*p.w;
    return p;
   }
   half4 Frag():SV_Target{return _Color;}
   ENDHLSL
  }
 }
}
