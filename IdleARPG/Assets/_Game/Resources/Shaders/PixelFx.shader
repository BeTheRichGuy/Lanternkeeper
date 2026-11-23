// 픽셀 VFX (희승 11-23: "VFX가 너무 단순해", docs/research/1123_pixel_vfx.md): procedural effect shapes — a shockwave
// ring, a slash smear, an impact flare, a fireball blast, a ground field — worked out per ART pixel (the quad's uv is
// snapped to its own pixel grid, so the shape steps like drawn pixel art at any zoom) and painted on a five-tone element
// ramp (dark rim → mid → bright → hot → white core) with a 4×4 ordered dither between tones; the two hottest tones are
// pushed past 1 so the camera's bloom catches them. _Add blends from alpha-over (0) to pure additive light (1).
Shader "Lanternkeeper/PixelFx"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Kind ("Kind", Float) = 0
        _T ("Progress 0..1", Float) = 0
        _Seed ("Seed", Float) = 0
        _Px ("Art pixels across (x, y)", Vector) = (64, 64, 0, 0)
        _Angle ("Angle (rad)", Float) = 0
        _Boost ("HDR boost of the hot tones", Float) = 2
        _Add ("Additive 0..1", Float) = 0.35
        _Alpha ("Alpha", Float) = 1
        _Param ("Kind parameter", Float) = 0
        _C0 ("Ramp 0 dark", Color) = (0.2, 0.05, 0.02, 1)
        _C1 ("Ramp 1", Color) = (0.7, 0.2, 0.05, 1)
        _C2 ("Ramp 2", Color) = (1, 0.48, 0.12, 1)
        _C3 ("Ramp 3", Color) = (1, 0.76, 0.29, 1)
        _C4 ("Ramp 4 core", Color) = (1, 0.95, 0.78, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Kind, _T, _Seed, _Angle, _Boost, _Add, _Alpha, _Param;
            float4 _Px;
            half4 _C0, _C1, _C2, _C3, _C4;
        CBUFFER_END

        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.uv = v.uv;
            return o;
        }

        float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
        }
        float Fbm(float2 p) { return Noise(p) * 0.55 + Noise(p * 2.03 + 7.1) * 0.3 + Noise(p * 4.1 + 3.7) * 0.15; }
        float EaseOut(float t) { t = saturate(t); return 1.0 - (1.0 - t) * (1.0 - t) * (1.0 - t); }

        static const float Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // 0 — shockwave: a ragged ring running out, hot on its outer lip (no fill: a faint one dithered into a dotted slab).
        float Ring(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x);
            float R = lerp(0.12, 0.96, EaseOut(t * 1.15));
            float w = lerp(0.2, 0.045, t);
            float d = (r - R) / w;
            // A solid band with a thin dithered lip (a linear falloff spread the dither into a coarse halftone, review 2).
            float v = pow(saturate(1.0 - abs(d)), 0.55) * (d > 0 ? 1.0 : 0.82);
            float n = Noise(float2(a * 5.0 + _Seed * 13.0, t * 4.0 + _Seed));
            v *= lerp(1.0, smoothstep(0.15, 0.75, n), saturate(t * 1.3));
            return v * (1.0 - smoothstep(0.55, 1.0, t));
        }

        // 1 — slash: a crescent swept around the centre (head ahead, tapering tail = the smear), eaten by noise as it fades.
        float Slash(float2 p, float t)
        {
            float c = cos(-_Angle), s = sin(-_Angle);
            p = float2(p.x * c - p.y * s, p.x * s + p.y * c);
            float r = length(p), a = atan2(p.y, p.x);
            float sweep = _Param > 0 ? _Param : 1.35;          // half the arc, radians
            float head = lerp(-sweep, sweep, EaseOut(t * 2.2));
            float tail = head - lerp(0.35, 2.0 * sweep, EaseOut(t * 1.6));
            if (a > head || a < tail) return 0;
            float k = (a - tail) / max(head - tail, 0.001);     // 0 tail .. 1 head
            float rc = 0.68 + 0.08 * k;
            float th = lerp(0.03, 0.25, k * k) * (1.0 - 0.5 * t);
            float d = (r - rc) / th;
            float v = saturate(1.0 - abs(d)) * lerp(0.55, 1.0, k);
            v *= d > 0 ? 1.15 : 0.9;                            // the outer edge cuts hottest
            float n = Noise(float2(a * 7.0 + _Seed * 9.0, r * 9.0));
            v *= lerp(1.0, smoothstep(0.25, 0.8, n), smoothstep(0.35, 0.9, t));
            return v * (1.0 - smoothstep(0.7, 1.0, t));
        }

        // 2 — impact flare: a white core and four to six spikes, all over in a few frames.
        // A filled star (the drawn pixel "impact": bold, solid, white at the heart, dark at the tips) that pops out in the
        // first fifth of its life and then shrinks and splits, with thin rays running past it for the first frames.
        float Flare(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x) + _Seed * 6.283;
            float spikes = _Param > 0 ? _Param : 4.0;
            float grow = saturate(t / 0.18);
            float size = lerp(0.35, 1.0, grow) * lerp(1.0, 0.55, saturate((t - 0.18) / 0.82));
            float lobe = pow(abs(cos(a * spikes * 0.5)), 3.0);
            float R = size * (0.38 + 0.62 * lobe);
            float star = saturate(1.0 - r / max(R, 0.01));
            star = pow(star, lerp(0.7, 1.4, t));                        // fuller at the peak, thinner as it dies
            float split = smoothstep(0.45, 0.9, t) * step(Noise(float2(a * 4.0 + _Seed * 17.0, r * 7.0)), 0.45);
            float rays = pow(abs(cos(a * spikes * 0.5 + 0.785)), 40.0) * saturate(1.0 - r / 0.98) * (1.0 - saturate(t / 0.35));
            float v = max(star * 1.25 * (1.0 - split), rays * 0.9);
            return v * (1.0 - smoothstep(0.65, 1.0, t));
        }

        // 3 — blast: a turbulent ball of fire that swells, hollows out from the middle and breaks into smoke-dark tongues.
        float Blast(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x);
            float n = Fbm(float2(a * 2.2 + _Seed * 11.0, r * 3.0 - t * 3.0));
            float R = lerp(0.2, 0.95, EaseOut(t * 1.4)) * (0.78 + 0.32 * n);
            float body = saturate(1.0 - r / max(R, 0.01));
            float hollow = smoothstep(0.0, 0.85, t) * saturate(1.0 - r / max(R * 0.75, 0.01));
            float v = saturate(body * 1.2 - hollow * 1.3) * (0.6 + 0.5 * n);
            v = max(v, saturate(1.0 - r / 0.3) * (1.0 - smoothstep(0.0, 0.35, t)) * 1.3); // the first white flash
            v = smoothstep(0.18, 1.0, v);   // a clean edge: the faint outer values dithered into a cheap haze (11-23)
            return v * (1.0 - smoothstep(0.6, 1.0, t));
        }

        // 4 — ground field: a rim and a slow churn inside, in and out over its life (consecration, rot, frost ground).
        float Field(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x);
            float time = _Time.y * 0.6 + _Seed * 10.0;
            float churn = Fbm(p * 3.2 + float2(time * 0.35, -time * 0.22));
            float inside = step(r, 0.97);
            float rim = saturate(1.0 - abs(r - 0.9) / 0.06) * (0.75 + 0.25 * sin(a * 12.0 + time * 2.0));
            float v = max(inside * smoothstep(0.35, 0.95, churn) * 0.62, rim);
            float life = smoothstep(0.0, 0.12, t) * (1.0 - smoothstep(0.8, 1.0, t));
            return v * life;
        }

        // 5 — scorch: a burnt blot left on the floor, its cracks glowing at first and cooling to the ramp's dark tones.
        float Scorch(float2 p, float t)
        {
            float r = length(p);
            float n = Fbm(p * 2.6 + _Seed * 9.0);
            float blot = smoothstep(0.62, 0.3, r + (n - 0.5) * 0.55);
            float ridge = 1.0 - abs(Noise(p * 5.5 + _Seed * 3.0) * 2.0 - 1.0);
            float crack = step(0.9, ridge) * blot * (1.0 - smoothstep(0.0, 0.45, t));
            float v = max(blot * 0.38, crack * 0.78);
            return v * (1.0 - smoothstep(0.7, 1.0, t));
        }

        // 6 — beam: a column of light falling from the top of its quad to the floor, a flash at the full column, then it
        // thins out (heaven's spears, a meteor's fall, a lightning strike). The quad's bottom edge is the floor.
        float Beam(float2 p, float t)
        {
            float fall = saturate(t / 0.3);
            float head = lerp(1.0, -1.0, fall);
            float above = step(head, p.y);
            float flash = smoothstep(0.25, 0.35, t) * (1.0 - smoothstep(0.35, 1.0, t));
            float w = lerp(0.12, 0.3, flash) * (1.0 - 0.6 * smoothstep(0.5, 1.0, t)) + 0.04;
            float jag = (Noise(float2(p.y * 6.0 + _Seed * 13.0, floor(t * 24.0))) - 0.5) * 0.25 * _Param;  // lightning wobble
            float x = abs(p.x - jag) / w;
            float v = saturate(1.0 - x) * above * lerp(0.55, 1.15, saturate((p.y - head + 0.3) / 1.3) * 0.4 + flash);
            v = max(v, saturate(1.0 - length(float2(p.x * 1.2, (p.y + 1.0) * 3.0))) * flash * 1.2);   // the splash at its foot
            return v * (1.0 - smoothstep(0.75, 1.0, t));
        }

        // 7 — sigil: a summoning circle laid on the floor under a great cast — two rings, a band of rune ticks turning
        // between them and a star drawn across — that opens, holds and burns out.
        float Sigil(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x);
            float open = EaseOut(saturate(t / 0.25));
            float spin = t * 2.2 + _Seed * 6.283;
            float outer = saturate(1.0 - abs(r - 0.9 * open) / 0.035);
            float inner = saturate(1.0 - abs(r - 0.66 * open) / 0.03);
            float band = step(0.7 * open, r) * step(r, 0.86 * open);
            float ticks = band * step(0.72, frac((a + spin) * 12.0 / 6.283)) * step(0.3, Noise(float2(floor((a + spin) * 12.0 / 6.283), _Seed * 7.0)));
            // A five-point star: the distance to the five chords of the inner circle.
            float star = 0;
            for (int k = 0; k < 5; k++)
            {
                float a0 = -spin * 0.5 + k * 2.513, a1 = a0 + 2.513 * 2.0;
                float2 p0 = float2(cos(a0), sin(a0)) * 0.66 * open, p1 = float2(cos(a1), sin(a1)) * 0.66 * open;
                float2 pa = p - p0, ba = p1 - p0;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                star = max(star, saturate(1.0 - length(pa - ba * h) / 0.025));
            }
            float v = max(max(outer, inner) * 0.95, max(ticks * 0.8, star * 0.75));
            float burn = smoothstep(0.7, 1.0, t);
            return v * (1.0 - burn) + v * burn * step(0.6, Noise(p * 9.0 + t * 5.0)) * (1.0 - burn);
        }

        // 8 — warning: where a blow will land. A rim of marching dashes, a dithered disc filling out to it, one flash as
        // it lands (d4 §6.2: one grammar for every danger, readable through any amount of the hero's own effects).
        float Warn(float2 p, float t)
        {
            float r = length(p), a = atan2(p.y, p.x);
            float rim = saturate(1.0 - abs(r - 0.92) / 0.06);
            float dash = step(0.35, frac(a * 16.0 / 6.283 - _Time.y * 1.5));
            float fill = step(r, 0.92 * EaseOut(t)) * (0.32 + 0.25 * t);
            float flash = step(0.86, t) * step(t, 0.95) * step(r, 0.95);
            return max(max(rim * lerp(0.55, 0.9, dash), fill), flash * 1.0);
        }

        half4 Frag(Varyings i) : SV_Target
        {
            float2 px = max(_Px.xy, 1.0);
            float2 cell = floor(i.uv * px);
            float2 uv = (cell + 0.5) / px;                       // one value per art pixel
            float2 p = uv * 2.0 - 1.0;
            float v;
            int kind = (int)round(_Kind);
            if (kind == 0) v = Ring(p, _T);
            else if (kind == 1) v = Slash(p, _T);
            else if (kind == 2) v = Flare(p, _T);
            else if (kind == 3) v = Blast(p, _T);
            else if (kind == 4) v = Field(p, _T);
            else if (kind == 5) v = Scorch(p, _T);
            else if (kind == 6) v = Beam(p, _T);
            else if (kind == 7) v = Sigil(p, _T);
            else v = Warn(p, _T);
            // Nothing may reach the quad's edge: a noisy blast running past it showed as a square block (11-23).
            float edge = 1.0 - max(abs(p.x), abs(p.y));
            v *= saturate(edge * 7.0);
            // A scorch is not light: a solid near-black burn (no dither, no tone ladder) with only its cracks glowing.
            if (kind == 5)
            {
                if (v < 0.2) return 0;
                half3 burn = v >= 0.6 ? _C3.rgb * _Boost : _C0.rgb * 0.3h;
                half ba = (v >= 0.6 ? 1.0h : 0.8h) * _Alpha;
                return half4(burn * ba, ba);
            }
            // Five tones with an ordered dither between them (classic hand-dithered pixel FX).
            float dither = (Bayer[((int)cell.x & 3) + ((int)cell.y & 3) * 4] + 0.5) / 16.0 - 0.5;
            // Dither only where a shape thins out (its outer fifth): inside, the tones sit solid (review 11-23: a dithered
            // middle read as coarse 2-3 px cells against 1 px characters).
            float q = pow(saturate(v), 1.35) * 5.0 + dither * 0.9 * saturate((1.0 - v) * 2.5); // the white core stays small
            if (q < 0.55) return 0;
            int band = (int)clamp(floor(q), 0, 4);
            half3 col = band == 0 ? _C0.rgb : band == 1 ? _C1.rgb : band == 2 ? _C2.rgb : band == 3 ? _C3.rgb * lerp(1.0, _Boost, 0.3) : _C4.rgb * _Boost;
            half a = (band == 0 ? 0.8 : 1.0) * _Alpha;
            return half4(col * a, a * (1.0 - _Add));
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
