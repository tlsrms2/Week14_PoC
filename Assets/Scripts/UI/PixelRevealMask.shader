Shader "Week14/UI/PixelRevealMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _RevealProgress ("Reveal Progress", Range(0,1)) = 1
        _PanelRect ("Panel Rect", Vector) = (0,0,1,1)
        _GridSize ("Grid Size", Vector) = (1,1,0,0)
        _CellWindow ("Cell Window", Float) = 0.16
        _EdgeRandomness ("Edge Randomness", Float) = 0.14
        _MaxCellDelay ("Max Cell Delay", Float) = 0.84
        _RevealTintColor ("Reveal Tint Color", Color) = (0.5,1,1,1)
        _RevealTintStrength ("Reveal Tint Strength", Range(0,1)) = 1
        _RevealTintDuration ("Reveal Tint Duration", Float) = 0.22

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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _RevealProgress;
            float4 _PanelRect;
            float4 _GridSize;
            float _CellWindow;
            float _EdgeRandomness;
            float _MaxCellDelay;
            fixed4 _RevealTintColor;
            float _RevealTintStrength;
            float _RevealTintDuration;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.localPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Random01(float seed)
            {
                return frac(sin(seed * 12.9898) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float2 panelSize = max(_PanelRect.zw, float2(0.0001, 0.0001));
                float2 panelUv = saturate((IN.localPosition.xy - _PanelRect.xy) / panelSize);
                float columns = max(1.0, floor(_GridSize.x + 0.5));
                float rows = max(1.0, floor(_GridSize.y + 0.5));
                float column = min(columns - 1.0, floor(panelUv.x * columns));
                float row = min(rows - 1.0, floor((1.0 - panelUv.y) * rows));
                float index = row * columns + column;

                float2 cellCenter = float2((column + 0.5) / columns, 1.0 - ((row + 0.5) / rows));
                float2 centered = (cellCenter - 0.5) * 2.0;
                float distance = saturate(length(centered) * 0.70710678);
                float randomOffset = (Random01(index * 17.17 + 3.31) - 0.5) * _EdgeRandomness;
                float delay = saturate(distance + randomOffset) * _MaxCellDelay;
                float localReveal = saturate((_RevealProgress - delay) / max(0.0001, _CellWindow));
                float visible = max(step(0.0001, localReveal), step(0.9999, _RevealProgress));
                color.a *= visible;

                float cellAge = max(0.0, _RevealProgress - delay);
                float tintDuration = max(0.0001, _RevealTintDuration);
                float tintFade = 1.0 - smoothstep(tintDuration * 0.65, tintDuration, cellAge);
                float tintAmount = visible * tintFade * _RevealTintStrength;
                color.rgb = lerp(color.rgb, _RevealTintColor.rgb, tintAmount);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
