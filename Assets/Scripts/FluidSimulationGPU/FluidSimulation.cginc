// --- Constants --- //
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
