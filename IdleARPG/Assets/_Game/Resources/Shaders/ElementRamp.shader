// 재기획 §4.6 원소 문법: one VFX sheet drawn in another element's colours. The sprite's brightness picks a colour on a
// three-stop ramp (dark → mid → light) — a skill whose element a glyph converted shows its new element, not the colour
// its sheet was painted in. Alpha and the vertex colour (fade, echo tint) pass through.
Shader "Lanternkeeper/ElementRamp"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Dark ("Dark", Color) = (0.1, 0.1, 0.1, 1)
        _Mid ("Mid", Color) = (0.5, 0.5, 0.5, 1)
        _Light ("Light", Color) = (1, 1, 1, 1)
        _C0 ("Ramp 0", Color) = (0, 0, 0, 0)
        _C1 ("Ramp 1", Color) = (0, 0, 0, 0)
        _C2 ("Ramp 2", Color) = (0, 0, 0, 0)
        _C3 ("Ramp 3", Color) = (0, 0, 0, 0)
        _C4 ("Ramp 4", Color) = (0, 0, 0, 0)
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
            half4 _Dark;
            half4 _Mid;
            half4 _Light;
            half4 _C0, _C1, _C2, _C3, _C4;
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
            half l = saturate(max(max(tex.r, tex.g), tex.b) * 0.6h + dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h)) * 0.4h);
            half3 rgb = l < 0.5h ? lerp(_Dark.rgb, _Mid.rgb, l * 2.0h) : lerp(_Mid.rgb, _Light.rgb, (l - 0.5h) * 2.0h);
            // 11-23: with the five-tone ramp set (the same as the procedural pixel FX), step onto it — no blends, a palette.
            if (_C4.a > 0)
            {
                int k = (int)clamp(floor(l * 5.0h), 0, 4);
                rgb = k == 0 ? _C0.rgb : k == 1 ? _C1.rgb : k == 2 ? _C2.rgb : k == 3 ? _C3.rgb : _C4.rgb;
            }
            rgb *= 1.0h + 0.9h * smoothstep(0.72h, 0.95h, l); // the hot core past 1 for bloom (11-23 VFX)
            return half4(rgb * i.color.rgb, tex.a * i.color.a);
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
