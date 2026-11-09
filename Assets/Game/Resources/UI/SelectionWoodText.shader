Shader "RhythmDojo/UI/Selection Wood Text"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelSize ("Pixel Size", Float) = 0
        _WoodGrain ("Wood Grain", Range(0, 1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 position : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PixelSize;
            float _WoodGrain;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = i.position.xy;
                float2 uvX = ddx(i.uv);
                float2 uvY = ddy(i.uv);
                if (_PixelSize > 1)
                {
                    // Sample each screen pixel block at its center, keeping the font's original glyphs.
                    float2 offset = (floor(i.vertex.xy / _PixelSize) + .5) * _PixelSize - i.vertex.xy;
                    uv += uvX * offset.x + uvY * offset.y;
                    p += ddx(p) * offset.x + ddy(p) * offset.y;
                }

                fixed4 glyph = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 color = glyph * i.color;
                if (_PixelSize > 1)
                {
                    // Light the upper-left rim and shade the opposite rim inside the glyph.
                    float2 diagonal = (uvX + uvY) * (_PixelSize * .75);
                    float upperLeft = tex2D(_MainTex, uv - diagonal).a + _TextureSampleAdd.a;
                    float lowerRight = tex2D(_MainTex, uv + diagonal).a + _TextureSampleAdd.a;
                    float relief = saturate(1 - upperLeft) * .18 - saturate(1 - lowerRight) * .26;
                    color.rgb *= .94 + relief;
                    float blockCoverage = smoothstep(.16, .74, glyph.a);
                    float originalCoverage = tex2D(_MainTex, i.uv).a + _TextureSampleAdd.a;
                    color.a = max(blockCoverage, saturate(originalCoverage) * .62) * i.color.a;
                }

                if (_WoodGrain > 0)
                {
                    float wave = p.y * 3.1 + sin(p.x * .045) * 1.7 + sin(p.x * .13) * .35;
                    float fibers = pow(.5 + .5 * sin(wave), 10);
                    float pores = .5 + .5 * sin(p.x * 1.9 + sin(p.y * .8) * 2.0);
                    color.rgb *= lerp(1, 1.06 - fibers * .24 - pores * .055, _WoodGrain);
                }
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.position.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - .001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
