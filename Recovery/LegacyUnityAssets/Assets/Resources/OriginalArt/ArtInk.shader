Shader "DrawArrow/OriginalInk" {
 Properties { _Color("Color",Color)=(0.36,0.24,0.12,1) }
 SubShader { Tags {"RenderType"="Opaque"} Pass { Cull Off ZWrite On
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;};struct v2f {float4 vertex:SV_POSITION;};float4 _Color;
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
 fixed4 frag(v2f i):SV_Target{return _Color;}
 ENDCG
 } }
}
