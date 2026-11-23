// 픽셀 파티클 (1123_pixel_vfx.md §4): one mesh of art-pixel squares a frame (sparks with pixel trails, embers, debris,
// smoke). The vertex colour carries the tone, past 1 for the hot ones so bloom catches them; the blend comes from the
// material (additive light for sparks and embers, alpha for smoke and debris).
Shader "Lanternkeeper/PixelParticle"
{
    Properties
    {
        [MainTexture] _MainTex ("Unused", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_Src] [_Dst]
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.color = v.color;
            return o;
        }

        // Additive: rgb is the light, scaled by alpha. Alpha blend: straight colour and alpha.
        half4 Frag(Varyings i) : SV_Target { return half4(i.color.rgb * i.color.a, i.color.a); }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
