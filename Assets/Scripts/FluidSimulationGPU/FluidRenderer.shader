Shader "Unlit/FluidRenderer" {
    Properties { }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            int2 resolution;
            int visualizationMode;
            sampler2D debugMap;

            sampler2D pressureMap;
            float pressureDisplayRange;
            fixed4 negativePressureColor;
            fixed4 positivePressureColor;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 RenderDebug(v2f i) {
                float4 val = tex2D(debugMap, i.uv);
                fixed4 col = fixed4(val.xyz, 1);
                return col;
            }

            fixed4 RenderPressure(v2f i) {
                float pressure = tex2D(pressureMap, i.uv).r;
                fixed4 col = pressure < 0 ? negativePressureColor : positivePressureColor;
                return fixed4(col.rgb * abs(pressure * pressureDisplayRange), col.a);
            }

            fixed4 frag (v2f i) : SV_Target {
                const int VISUALIZATION_MODE_DEBUG = 0;
                const int VISUALIZATION_MODE_PRESSURE = 1;

                fixed4 col = fixed4(1, 0, 1, 1);

                if (visualizationMode == VISUALIZATION_MODE_DEBUG) col = RenderDebug(i);
                else if (visualizationMode == VISUALIZATION_MODE_PRESSURE) col = RenderPressure(i);

                return col;
            }
            ENDCG
        }
    }
}
