// Actors (hero, monsters, bosses): unlit sprite with a 1-texel dark outline and a 1-texel rim on the top edge of the
// silhouette, so a figure always separates from the floor whatever the act's colours (docs/research/d4-visual-techniques.md §6.1).
// Vertex colour still multiplies the sprite (tint, lantern falloff, hit flash, fade); the outline fades with it.
Shader "Lanternkeeper/ActorSprite"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _OutlineColor ("Outline (rgb, a = strength)", Color) = (0.03, 0.02, 0.04, 0.8)
        _RimColor ("Top rim (rgb, a = strength)", Color) = (1, 0.95, 0.85, 0.4)
        _Lift ("Lift toward white (dark art)", Range(0, 1)) = 0
        _Flash ("Hit flash (rgb colour, a amount)", Color) = (1, 1, 1, 0)
        _Dissolve ("Dissolve (rgb burning edge, a amount)", Color) = (1, 0.5, 0.15, 0)
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
            float4 _MainTex_TexelSize;
            half4 _OutlineColor;
            half4 _RimColor;
            half _Lift;
            half4 _Flash;
            half4 _Dissolve;
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

        half A(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

        // 픽셀 VFX (희승 11-23): a body breaks up texel by texel instead of fading — a hash per art texel, mixed with a
        // slow top-down sweep, and a band just ahead of the gap burning in the killing element's colour (past 1 for bloom).
        float Cell(float2 c) { c = frac(c * float2(0.1031, 0.1137)); c += dot(c, c.yx + 33.33); return frac((c.x + c.y) * c.x); }
        float Smooth(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Cell(i), Cell(i + float2(1, 0)), f.x), lerp(Cell(i + float2(0, 1)), Cell(i + float2(1, 1)), f.x), f.y); }

        half4 Frag(Varyings i) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            float2 t = _MainTex_TexelSize.xy;
            half up = A(i.uv + float2(0, t.y));
            half around = max(max(A(i.uv + float2(t.x, 0)), A(i.uv - float2(t.x, 0))), max(up, A(i.uv - float2(0, t.y))));

            // Rim: an opaque texel with empty space right above it (the iso light comes from above).
            half rim = tex.a * saturate(1 - up) * _RimColor.a;
            half3 rgb = lerp(tex.rgb, _RimColor.rgb, rim);
            rgb = (rgb + (1 - rgb) * _Lift) * i.color.rgb; // art drawn too dark for its floor is lifted a step
            rgb = lerp(rgb, _Flash.rgb, _Flash.a); // hit flash: past the art's own brightness, which vertex colour cannot reach
            half body = tex.a * i.color.a;
            if (_Dissolve.a > 0)
            {
                float2 cell = floor(i.uv / t);                                     // per art texel, but a smooth field:
                float n = Smooth(cell * 0.22) * 0.75 + Cell(floor(cell * 0.5)) * 0.25; // it erodes in clumps, not salt
                float cut = _Dissolve.a * 1.1;
                if (n < cut - 0.07) { body = 0; }
                else if (n < cut) rgb = _Dissolve.rgb * 1.5;                    // the burning edge
            }

            // Outline: an empty texel next to an opaque one.
            half outline = saturate(around - tex.a) * _OutlineColor.a * i.color.a;
            half a = body + outline * (1 - body);
            half3 col = a > 0 ? (rgb * body + _OutlineColor.rgb * outline * (1 - body)) / max(a, 1e-4) : rgb;
            return half4(col, a);
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
