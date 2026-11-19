Shader "RhythmDojo/Journey Air"
{
    Properties
    {
        _Color ("Mist pigment", Color) = (.7,.8,.77,.2)
        _VisualTime ("Presentation time", Float) = 0
        _Progress ("Earned progress", Float) = 0
        _Energy ("Wind", Float) = 0
        _Rush ("Running wake", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Color;
            float _VisualTime, _Progress, _Energy, _Rush;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = floor(i.uv * float2(160,40)) / float2(160,40);
                float bend = sin(uv.x * 9 + _VisualTime * .17) * .08;
                float band = saturate(1 - abs(uv.y - .5 + bend) * 2);
                float edge = saturate(sin(uv.x * 3.14159));
                float plume = saturate(.7 + sin(uv.x * 15 - _VisualTime * .25) * .3);
                float opacity = band * band * edge * plume * _Color.a;
                opacity *= lerp(1, .03 + _Energy * .8, _Rush);
                return half4(_Color.rgb, opacity * (1 + _Energy * .3));
            }
            ENDHLSL
        }
    }
}
