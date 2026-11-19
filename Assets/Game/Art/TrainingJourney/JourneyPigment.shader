Shader "RhythmDojo/Journey Pigment"
{
    Properties
    {
        _MainTex ("Painted pixels", 2D) = "white" {}
        _Color ("Pigment", Color) = (1,1,1,1)
        _Sway ("Living foliage", Float) = 0
        _Wood ("Wood wear", Float) = 0
        _Sky ("Altitude atmosphere", Float) = 0
        _VisualTime ("Presentation time", Float) = 0
        _Progress ("Earned progress", Float) = 0
        _Energy ("Impulse", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color;
            float _Sway, _Wood, _Sky, _VisualTime, _Progress, _Energy;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 source : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float phase = unity_ObjectToWorld._m03 * .73;
                v.positionOS.x += _Sway * v.uv.y * v.uv.y * .012 *
                    sin(_VisualTime * 1.4 + v.uv.y * 5 + phase) * (1 + _Energy * .5);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.source = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 art = tex2D(_MainTex, i.uv) * _Color;
                clip(art.a - .45);
                float heightTint = _Sky * saturate(_Progress / .85);
                art.rgb *= lerp(float3(.81,.91,.94), float3(1.0,.98,.94), heightTint) * lerp(1, 1.06, _Energy);
                float y = floor(i.source.y * 160) / 160;
                float seam = .46 + sin(y * 35) * .022 + sin(y * 91) * .008;
                float crack = 1 - step(.003 + saturate(_Progress - .55) * .012, abs(i.source.x - seam));
                crack *= step(y, saturate((_Progress - .35) * 1.5));
                art.rgb *= 1 - crack * _Wood * .74;
                return half4(art.rgb, 1);
            }
            ENDHLSL
        }
    }
}
