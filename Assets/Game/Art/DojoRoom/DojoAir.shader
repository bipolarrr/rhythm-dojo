Shader "RhythmDojo/Dojo Air"
{
    Properties
    {
        _Opening ("Opening", Float) = 0
        _VisualTime ("Music animation time", Float) = 0
        _Pulse ("Impact light", Float) = 0
        _Wind ("Wind", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Opening, _VisualTime, _Pulse, _Wind;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                v.positionOS.x *= .015 + _Opening;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float fade = pow(sin(i.uv.x*3.14159),2)*sin(i.uv.y*3.14159);
                return half4(.91,.78,.51,fade*(.025+.075*_Opening)*(1+_Pulse*.15));
            }
            ENDHLSL
        }
    }
}
