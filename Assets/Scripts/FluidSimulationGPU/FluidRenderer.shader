Shader "Unlit/FluidRenderer" {
    Properties {
    }
    SubShader {
        Tags{ "RenderType" = "Opaque" } LOD 100

            Pass {
            CGPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

// Velocity channels
#define VELOCITY_CHANNEL_X (0)
#define VELOCITY_CHANNEL_Y (1)
#define VELOCITY_CHANNEL_BOTH (2)

// Visualization modes
#define VISUALIZATION_MODE_DEBUG (0)
#define VISUALIZATION_MODE_VELOCITY (1)
#define VISUALIZATION_MODE_DIVERGENCE (2)
#define VISUALIZATION_MODE_PRESSURE (3)
#define VISUALIZATION_MODE_SMOKE (4)

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

            // Velocity
            sampler2D velocityMap;
            float velocityDisplayRange;
            int velocityChannel;

            // Divergence
            float divergenceDisplayRange;
            fixed4 negativeDivergenceColor;
            fixed4 positiveDivergenceColor;

            // Pressure
            sampler2D pressureMap;
            float pressureDisplayRange;
            fixed4 negativePressureColor;
            fixed4 positivePressureColor;

            // Smoke
            sampler2D smokeMap;
            float smokeDisplayRange;

            v2f vert(appdata v) {
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

            fixed4 RenderVelocity(v2f i) {
                float2 velocity = tex2D(velocityMap, i.uv).rg;
                fixed4 col = fixed4(0, 0, 0, 1);
                if (velocityChannel == VELOCITY_CHANNEL_X || velocityChannel == VELOCITY_CHANNEL_BOTH)
                    col.r = velocity.x * abs(velocity.x * velocityDisplayRange);
                if (velocityChannel == VELOCITY_CHANNEL_Y || velocityChannel == VELOCITY_CHANNEL_BOTH)
                    col.g = velocity.y * abs(velocity.y * velocityDisplayRange);
                return col;
            }

            fixed4 RenderDivergence(v2f i) {
                const float2 ds = float2(1.0 / resolution.x, 1.0 / resolution.y);
                float2 sample = tex2D(velocityMap, i.uv).rg;
                float velocityBottom = sample.y;
                float velocityLeft = sample.x;
                float velocityTop = tex2D(velocityMap, float2(i.uv.x, i.uv.y + ds.y)).y;
                float velocityRight = tex2D(velocityMap, float2(i.uv.x + ds.x, i.uv.y)).x;
                float divergence = (velocityTop - velocityBottom) + (velocityRight - velocityLeft);

                fixed4 col = divergence < 0 ? negativeDivergenceColor : positiveDivergenceColor;
                return fixed4(col.rgb * abs(divergence * divergenceDisplayRange), col.a);
            }

            fixed4 RenderPressure(v2f i) {
                float pressure = tex2D(pressureMap, i.uv).r;
                fixed4 col = pressure < 0 ? negativePressureColor : positivePressureColor;
                return fixed4(col.rgb * abs(pressure * pressureDisplayRange), col.a);
            }

            fixed4 RenderSmoke(v2f i) {
                float smoke = tex2D(smokeMap, i.uv).r;
                return fixed4(smoke * abs(smoke * smokeDisplayRange), 0, 0, 1.0);
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 col = fixed4(1, 0, 1, 1);

                if (visualizationMode == VISUALIZATION_MODE_DEBUG)
                    col = RenderDebug(i);
                else if (visualizationMode == VISUALIZATION_MODE_VELOCITY)
                    col = RenderVelocity(i);
                else if (visualizationMode == VISUALIZATION_MODE_DIVERGENCE)
                    col = RenderDivergence(i);
                else if (visualizationMode == VISUALIZATION_MODE_PRESSURE)
                    col = RenderPressure(i);
                else if (visualizationMode == VISUALIZATION_MODE_SMOKE)
                    col = RenderSmoke(i);
                return col;
            }
            ENDCG
        }
    }
}
