Shader "RhythmDojo/Dojo Surface"
{
    Properties
    {
        _MainTex ("Art", 2D) = "white" {}
        _Color ("Pigment", Color) = (1,1,1,1)
        _Unlit ("Distant atmosphere", Float) = 0
        _Cloth ("Cloth", Float) = 0
        _Opening ("Opening", Float) = 0
        _VisualTime ("Music animation time", Float) = 0
        _Pulse ("Impact light", Float) = 0
        _Wind ("Wind", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color;
            float _Unlit, _Cloth, _Opening, _VisualTime, _Pulse, _Wind;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 normal:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float edge = 1-v.uv.y;
                float phase = unity_ObjectToWorld._m03 * 1.19;
                v.positionOS.z += _Cloth * edge * edge * (.035*sin(_VisualTime*1.7+v.uv.y*4+phase) + .014*sin(_VisualTime*2.83+v.uv.x*3+phase*.7)) * _Wind;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 art = tex2D(_MainTex, i.uv) * _Color;
                clip(art.a - .6);
                float3 local = i.world - float3(1000,0,0);
                float lambert = saturate(dot(normalize(i.normal), normalize(float3(.4,.8,.2))));
                float openingX = local.x + (8-local.z)*.48;
                float gap = .014 + 2.8*_Opening;
                float sun = 1-smoothstep(gap-.045, gap+.045, abs(openingX));
                sun *= smoothstep(-2,1,local.z) * (1-smoothstep(7.94,8.1,local.z));
                sun *= saturate(i.normal.y);
                // Shadow of the lintel and slender cross rails, projected onto the actual floor.
                float rail = smoothstep(.035,.075,abs(frac((local.z+local.x*.1)*.68)-.5));
                sun *= lerp(.32,1,rail);
                float3 shade = float3(.49,.48,.45) + lambert*float3(.30,.28,.24);
                float sideWindow = (1-smoothstep(.7,1.5,abs(local.x-4.0+(local.z-4)*.18)));
                sideWindow *= (1-smoothstep(5,7,local.z))*smoothstep(-1,1,local.z)*saturate(i.normal.y);
                float lattice = smoothstep(.10,.15,abs(frac(local.z*.8+local.x*.15)-.5));
                shade += sideWindow*lattice*float3(.42,.34,.20);
                shade += sun*float3(1.10,.88,.52)*(1+_Pulse*.12);
                shade += _Opening*float3(.10,.10,.085);
                float fold = 1 - _Cloth * (.09+.06*sin(i.uv.x*25+_VisualTime*.8));
                art.rgb *= lerp(shade,1,_Unlit) * fold;
                return art;
            }
            ENDHLSL
        }
    }
}
