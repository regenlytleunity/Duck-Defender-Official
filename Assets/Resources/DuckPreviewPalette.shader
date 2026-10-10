Shader "Duck Defender/Duck Preview Palette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PlayerColor ("Player Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _PlayerColor;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 pixel = tex2D(_MainTex, i.uv);
                // Match the gameplay PlayerPalette: replace the yellow body,
                // preserving the original beak, outline and transparent pixels.
                float yellow = step(.3, pixel.r) * step(pixel.r * .8, pixel.g) * step(pixel.b, pixel.r * .65);
                pixel.rgb = lerp(pixel.rgb, _PlayerColor.rgb * max(pixel.r, pixel.g), yellow);
                return pixel * i.color;
            }
            ENDCG
        }
    }
}
