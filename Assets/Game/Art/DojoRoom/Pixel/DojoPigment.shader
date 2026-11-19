Shader "RhythmDojo/Dojo Pigment"
{
    Properties
    {
        _MainTex ("Painted pixels", 2D) = "white" {}
        _Color ("Pigment", Color) = (1,1,1,1)
        _RoomShade ("Interior light response", Range(0,1)) = 1
        _Cloth ("Cloth movement", Float) = 0
        _Opening ("Opening", Float) = 0
        _VisualTime ("Music animation time", Float) = 0
        _Pulse ("Impact light", Float) = 0
        _Wind ("Wind", Float) = 1
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
            float _RoomShade, _Cloth, _Opening, _VisualTime, _Pulse, _Wind;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float edge = 1 - v.uv.y;
                float phase = unity_ObjectToWorld._m03 * 1.19;
                v.positionOS.x += _Cloth * edge * edge * .016 *
                    sin(_VisualTime * 1.7 + v.uv.y * 4 + phase) * _Wind;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 art = tex2D(_MainTex, i.uv) * _Color;
                clip(art.a - .5);
                // All shapes, shadow bands and highlights belong to the source art.
                // Opening changes exposure only; no normal lighting or synthetic grain.
                float exposure = lerp(.56, 1, _Opening) + _Pulse * .025;
                art.rgb *= lerp(1, exposure, _RoomShade);
                return half4(art.rgb, 1);
            }
            ENDHLSL
        }
    }
}
