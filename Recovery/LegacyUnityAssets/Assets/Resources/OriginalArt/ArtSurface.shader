Shader "DrawArrow/OriginalSurface" {
 Properties { _Color("Color",Color)=(1,0.95,0.86,1) }
 SubShader { Tags {"RenderType"="Opaque"} Pass { Cull Back ZWrite On
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
 struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;};
 float4 _Color;
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
 fixed4 frag(v2f i):SV_Target{float l=saturate(dot(normalize(i.normal),normalize(float3(-0.45,0.85,-0.65))));return fixed4(_Color.rgb*(0.70+0.30*l),1);}
 ENDCG
 } }
}
