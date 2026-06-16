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

// --- Structures --- //
struct ParcelsData {
    int toRemove;
    float mass;
    float2 position;
    float2 velocity;
    float2 cx;
    float2 cy;
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
