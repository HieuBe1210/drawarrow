Shader "ArrowCircuit/BoardMaterial"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Shade ("Directional shading", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Shade;
            struct Varyings { float4 position : SV_POSITION; float light : TEXCOORD0; };
            Varyings Vertex(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                float3 normal = UnityObjectToWorldNormal(input.normal);
                output.light = .58 + .42 * saturate(dot(normal, float3(.36,.8,-.48)));
                return output;
            }
            fixed4 Fragment(Varyings input) : SV_Target
            {
                return fixed4(_Color.rgb * lerp(1, input.light, _Shade), _Color.a);
            }
            ENDCG
        }
    }
}
