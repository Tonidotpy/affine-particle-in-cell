Shader "Unlit/FluidRenderer" {
    Properties {
    }
    SubShader {
        Blend SrcAlpha OneMinusSrcAlpha
        Tags{ "RenderType" = "Opaque" } LOD 100

            Pass {
            CGPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

// Cell types
#define CELL_TYPE_SOLID (0)
#define CELL_TYPE_FLUID (1)

// Velocity channels
#define VELOCITY_CHANNEL_X (0)
#define VELOCITY_CHANNEL_Y (1)
#define VELOCITY_CHANNEL_BOTH (2)

// Visualization modes
#define VISUALIZATION_MODE_DEBUG (0)
#define VISUALIZATION_MODE_VELOCITY (1)
#define VISUALIZATION_MODE_DIVERGENCE (2)
#define VISUALIZATION_MODE_PRESSURE (3)
#define VISUALIZATION_MODE_TEMPERATURE (4)
#define VISUALIZATION_MODE_SMOKE (5)

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

            // Obstacles
            sampler2D cellType;
            fixed4 obstacleColor;

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

            // Temperature
            float temperatureDisplayRange;
            fixed4 negativeTemperatureColor;
            fixed4 positiveTemperatureColor;

            // Smoke
            sampler2D smokeMap;
            float smokeDisplayRange;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float RandomNumber(float2 p) {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
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
                    col.r = velocity.x * velocityDisplayRange;
                if (velocityChannel == VELOCITY_CHANNEL_Y || velocityChannel == VELOCITY_CHANNEL_BOTH)
                    col.g = velocity.y * velocityDisplayRange;
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

            fixed4 RenderTemperature(v2f i) {
                float temperature = tex2D(smokeMap, i.uv).a;
                fixed4 col = temperature < 0 ? negativeTemperatureColor : positiveTemperatureColor;
                return fixed4(col.rgb * abs(temperature * temperatureDisplayRange), col.a);
            }

            fixed4 RenderSmoke(v2f i) {
                float3 smoke = tex2D(smokeMap, i.uv).rgb;
                return fixed4(smoke * abs(smoke * smokeDisplayRange), 1);
            }

            fixed4 RenderObstacle(v2f i, fixed4 col) {
                uint3 type = tex2D(cellType, i.uv).xyz;
                if (type.x == CELL_TYPE_SOLID) {
                    // Check if it is an obstacle from its index
                    if (type.y < 0) { col = obstacleColor; }
                    else {
                        // Check if it is an obstacle edge
                        if (type.z > 0) { col = obstacleColor; }
                        else {
                            col = fixed4(
                                RandomNumber(float2(type.y, -1)),
                                RandomNumber(float2(type.y, 0)),
                                RandomNumber(float2(type.y, 1)),
                                0.3
                            );
                        }
                    }
                }
                return col;
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
                else if (visualizationMode == VISUALIZATION_MODE_TEMPERATURE)
                    col = RenderTemperature(i);
                else if (visualizationMode == VISUALIZATION_MODE_SMOKE)
                    col = RenderSmoke(i);

                if (visualizationMode != VISUALIZATION_MODE_TEMPERATURE)
                    col = RenderObstacle(i, col);
                return col;
            }
            ENDCG
        }
    }
}
