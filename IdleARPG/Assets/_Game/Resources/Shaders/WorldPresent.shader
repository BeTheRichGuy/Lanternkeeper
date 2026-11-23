// 화면 왜곡 (1123_pixel_vfx.md §4: "열기·충격파는 present 단계에서"): the world picture drawn over the screen, bent by up
// to eight shockwave ripples — each an iso ellipse running out from its centre, pushing the picture outward on its front
// and pulling it back behind. Offsets are rounded to whole art pixels so the floor shifts in blocks, like pixel art, and
// a whisper of colour split rides the front.
Shader "Lanternkeeper/WorldPresent"
{
    Properties
    {
        [PerRendererData] _MainTex ("World", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Cam;            // world left, bottom, width, height of the picture
            int _RipN;
            float4 _Rip[8];         // centre x, y (world), radius now (world, along x), strength (world units)
            float4 _RipW[8];        // front width (world), -, -, -
            float _ArtPx;           // world units per art pixel
            fixed4 _Impact;         // 임팩트 프레임: rgb the lit colour, a how far the picture turns two-tone

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 wp = _Cam.xy + i.uv * _Cam.zw;
                float2 off = 0;
                float front = 0;
                for (int k = 0; k < 8; k++)
                {
                    if (k >= _RipN) break;
                    float2 d = wp - _Rip[k].xy;
                    float2 e = float2(d.x, d.y * 2.0);          // the iso floor: a circle is twice as wide as tall
                    float r = length(e);
                    float x = (r - _Rip[k].z) / max(_RipW[k].x, 1e-4);
                    // Outward on the front, inward just behind it (a refraction ring, not a smear).
                    float prof = x * exp(-x * x * 1.6) * 1.6;
                    float2 dir = r > 1e-4 ? e / r : 0;
                    off += float2(dir.x, dir.y * 0.5) * prof * _Rip[k].w;
                    front += exp(-x * x * 3.0) * saturate(_Rip[k].w * 20.0);
                }
                off = round(off / _ArtPx) * _ArtPx;            // whole art pixels
                float2 uvOff = off / _Cam.zw;
                float split = 0; // the colour split read as a rainbow bleed over corpse piles (review 2, 11-23): off
                fixed4 c;
                c.r = tex2D(_MainTex, i.uv - uvOff + float2(split, 0)).r;
                c.g = tex2D(_MainTex, i.uv - uvOff).g;
                c.b = tex2D(_MainTex, i.uv - uvOff - float2(split, 0)).b;
                c.a = 1;
                // An impact frame: for a few frames the picture turns two-tone — its lit half in the blow's colour, the
                // rest near black (the anime / fighting-game beat that sells an ultimate or a boss's fall).
                if (_Impact.a > 0)
                {
                    float l = dot(c.rgb, float3(0.299, 0.587, 0.114));
                    fixed3 two = l > 0.32 ? _Impact.rgb : fixed3(0.03, 0.02, 0.04);
                    c.rgb = lerp(c.rgb, two, _Impact.a);
                }
                return c * i.color;
            }
            ENDCG
        }
    }
}
