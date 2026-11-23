// 픽셀 VFX (희승 11-23): a VFX sheet in its own colours with its near-white core pushed past 1 for bloom; alpha and the
// vertex colour (fade, echo tint) pass through.
Shader "Lanternkeeper/FxSprite"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Hot ("Boost of the hottest pixels", Float) = 1.9
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half _Hot;
        CBUFFER_END

        struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.color = v.color;
            o.uv = v.uv;
            return o;
        }

        half4 Frag(Varyings i) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            half l = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));
            // 1123_pixel_vfx.md §2: only the near-white core goes past 1, so the camera's bloom lights the heart of the
            // drawing and leaves its colours and dark rim as drawn.
            half hot = 1.0h + (_Hot - 1.0h) * smoothstep(0.72h, 0.95h, l);
            return half4(tex.rgb * hot * i.color.rgb, tex.a * i.color.a);
        }
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
