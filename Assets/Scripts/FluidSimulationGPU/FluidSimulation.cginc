// --- Constants --- //
#define PI (3.14159265359)

#define UINT_MAX (0xFFFFFFFF)
#define INT_MAX (0x7FFFFFFF)

// Cell type
#define CELL_TYPE_SOLID (0)
#define CELL_TYPE_FLUID (1)

// Axis
#define AXIS_ALL (-1);
#define AXIS_X (0);
#define AXIS_Y (1);

// Flow data bit position
#define FLOW_TOP_BIT_POS (31)
#define FLOW_BOTTOM_BIT_POS (30)
#define FLOW_RIGHT_BIT_POS (29)
#define FLOW_LEFT_BIT_POS (28)

// Edge data bit position
#define EDGE_TOP_BIT_POS (3)
#define EDGE_BOTTOM_BIT_POS (2)
#define EDGE_RIGHT_BIT_POS (1)
#define EDGE_LEFT_BIT_POS (0)

// --- Structures --- //
struct ParcelsData {
    int toRemove;
    float mass;
    float2 position;
    float2 velocity;
    float2 cx;
    float2 cy;
};

struct ObstacleIndex {
    int vertex;
    int triang;
};

struct ObstacleData {
    bool isSmokeSource;
    float2 velocitySource;
    float smokeRateMultiplier;
    float smokeRate;
    float temperature;
};

// --- Utilities --- //

#define SWAP(a, b, type) { type aux = a; a = b; b = aux; }

float Random1(in float2 uv) {
    float2 noise = frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453);
    return abs(noise.x + noise.y) * 0.5;
}

float2 Random2(in float2 uv) {
    float noiseX = frac(sin(dot(uv, float2(12.9898,78.233) * 2.0)) * 43758.5453);
    float noiseY = sqrt(1 - noiseX * noiseX);
    return float2(noiseX, noiseY);
}
