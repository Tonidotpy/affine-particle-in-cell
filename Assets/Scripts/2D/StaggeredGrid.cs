using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Fluid staggered grid implementation
/// </summary>
/// <para>
/// This staggered grid is a fixed size grid composed of square Cells where
/// each Cell contains the information about the fluid contained inside them.
/// The model variables of a Cell are:
/// <list type="bullet">
///     <item>
///         <term>Mass</term>
///         <description>
///         Mass of the fluid contained inside the Cell, it is placed
///         at the center of the Cell
///         </description>
///     </item>
///     <item>
///         <term>Pressure</term>
///         <description>
///         Pressure of the fluid contained inside the Cell, it is placed
///         at the center of the Cell
///         </description>
///     </item>
///     <item>
///         <term>Velocity</term>
///         <description>
///         Velocity of the fluid contained inside the Cell, the velocity is
///         stored using four scalar values: two for the x velocity located on
///         the left and right edges of the Cell and two for the y velocity
///         located on the top and bottom edges.
///         </description>
///     </item>
/// </list>
/// </para>
public class StaggeredGrid {
    private int2 _size;
    private NativeArray<float> _mass;
    private NativeArray<float> _pressure;
    private NativeArray<float> _velocityX;
    private NativeArray<float> _velocityY;

    /*
     * Auxiliary arrays used for momentum transfer
     * Momentum and mass are located at the Grid Nodes
     */
    private NativeArray<float2> _nodeMomentum;
    private NativeArray<float> _nodeMass;

    /*
     * Auxiliary array used to store divergence used to solve the pressure problem
     */
    private NativeArray<float> _divergence;

    public int2 Size { get { return _size - 2; } }  // Size attribute excludes the "ghost Cells" layer
    public float CellSize { get; }
    public float CellArea { get { return CellSize * CellSize; } }

    public NativeArray<float> Mass { get { return _mass; } }
    public NativeArray<float> Pressure { get { return _pressure; } set { _pressure = value; } }
    public NativeArray<float> VelocityX { get { return _velocityX; } }
    public NativeArray<float> VelocityY { get { return _velocityY; } }

    public NativeArray<float2> NodeMomentum { get { return _nodeMomentum; } }
    public NativeArray<float> NodeMass { get { return _nodeMass; } }
    public NativeArray<float> Divergence { get { return _divergence; } }

    /// <summary>
    /// Staggered Grid constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="size">The size of the grid (width, height)</param>
    /// <param name="cellSize">Size of a single Cell of the Grid</param>
    /// <param name="allocator">The used memory allocator</param>
    public StaggeredGrid(int2 size, float cellSize, Allocator allocator) {
        // Add a layer of "ghost Cells" around the grid
        _size = size + 2;
        CellSize = cellSize;

        // Only the center mass and pressure requires the ghost layer
        int area = _size.x * _size.y;
        _mass = new NativeArray<float>(area, allocator);
        _pressure = new NativeArray<float>(area, allocator);

        int velocityXSize = (Size.x + 1) * Size.y;
        int velocityYSize = Size.x * (Size.y + 1);
        _velocityX = new NativeArray<float>(velocityXSize, allocator);
        _velocityY = new NativeArray<float>(velocityYSize, allocator);

        int nodes = (Size.x + 1) * (Size.y + 1);
        _nodeMomentum = new NativeArray<float2>(nodes, allocator);
        _nodeMass = new NativeArray<float>(nodes, allocator);

        int boundedArea = Size.x * Size.y;
        _divergence = new NativeArray<float>(boundedArea, allocator);
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (_mass.IsCreated) _mass.Dispose();
        if (_pressure.IsCreated) _pressure.Dispose();
        if (_velocityX.IsCreated) _velocityX.Dispose();
        if (_velocityY.IsCreated) _velocityY.Dispose();

        if (_nodeMomentum.IsCreated) _nodeMomentum.Dispose();
        if (_nodeMass.IsCreated) _nodeMass.Dispose();
        if (_divergence.IsCreated) _divergence.Dispose();
    }

    /// <summary>
    /// Resets grid masses and velocity values
    /// </summary>
    public void Reset() { 
        unsafe {
            long byteSize = _mass.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(_mass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = _nodeMomentum.Length * (long)UnsafeUtility.SizeOf<float2>();
            UnsafeUtility.MemClear(_nodeMomentum.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = _nodeMass.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(_nodeMass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = _velocityX.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(_velocityX.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = _velocityY.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(_velocityY.GetUnsafePtr(), byteSize);
        }
    }

    /// <summary>
    /// Transfer mass to the grid
    /// </summary>
    /// <param name="parcels">The parcel which mass need to be transfered</param>
    public void TransferMass(Parcels parcels) {
        for (int i = 0; i < parcels.Count; ++i) {
            float2 baseParcel = parcels.Position[i] / CellSize - 0.5f;

            /*
             * Calculate the index of the bottom-left Cell inside the 2x2 block
             * of Cells that surround the Parcel
             * Shift by one due to the ghost layer
             */
            int2 index = (int2)math.floor(baseParcel + 1);
            
            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 t = baseParcel - (index - 1);

            /*
             * Distribute mass over the Grid Cells via bilinear interpolation
             * based on the position of the Parcel
             */
            int[] cellIndex = new int[] {
                 index.x      * _size.y +  index.y     , // Bottom-Left
                (index.x + 1) * _size.y +  index.y     , // Bottom-Right
                 index.x      * _size.y + (index.y + 1), // Top-Left
                (index.x + 1) * _size.y + (index.y + 1)  // Top-Right
            };
            float[] weights = new float[] {
                (1 - t.x) * (1 - t.y), // Bottom-Left
                     t.x  * (1 - t.y), // Bottom-Right
                (1 - t.x) *      t.y , // Top-Left
                     t.x  *      t.y   // Top-Right
            };
            for (int j = 0; j < cellIndex.Length; ++j) {
                _mass[cellIndex[j]] += weights[j] * parcels.Mass[i];
            }
        }
    }

    /// <summary>
    /// Transfer momentum to the grid
    /// </summary>
    /// <param name="parcels">The parcel which mass need to be transfered</param>
    public void TransferMomentum(Parcels parcels) {
        for (int i = 0; i < parcels.Count; ++i) {
            float2 rescaledParcelPosition = parcels.Position[i] / CellSize;

            /*
             * Calculate the index of the bottom-left Cell inside the 2x2 block
             * of Cells that surround the Parcel
             * Shift by one due to the ghost layer
             */
            int2 index = (int2)math.floor(rescaledParcelPosition);

            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 t = rescaledParcelPosition - index;

            /*
             * Calculate bilinear interpolation weights based on Parcel position
             */
            float[] weights = new float[] {
                (1 - t.x) * (1 - t.y), // Bottom-Left
                     t.x  * (1 - t.y), // Bottom-Right
                (1 - t.x) *      t.y , // Top-Left
                     t.x  *      t.y   // Top-Right
            };
            /*
             * Calculate position of Cell nodes which enclose the Parcel
             */
            int2[] rescaledNodePosition = new int2[] {
                (index                 ), // Bottom-Left
                (index + new int2(1, 0)), // Bottom-Right
                (index + new int2(0, 1)), // Top-Left
                (index +             1 )  // Top-Right
            };
            int[] nodeIndex = new int[rescaledNodePosition.Length];
            for (int j = 0; j < rescaledNodePosition.Length; ++j) {
                nodeIndex[j] = math.mad(
                    rescaledNodePosition[j].x,
                    Size.y + 1,
                    rescaledNodePosition[j].y
                );
            }

            /*
             * Add weighted momentum to Grid Nodes
             */
            for (int j = 0; j < nodeIndex.Length; ++j) {
                // Calculate velocity at node position
                float2 nodeVelocity = parcels.Velocity[i] + math.mul(
                    parcels.AffineState[i],
                    math.mad(
                        rescaledNodePosition[j],
                        CellSize,
                        -parcels.Position[i]
                    )
                );

                // Calculate mass distribution and momentum at the Nodes
                float nodeMass = weights[j] * parcels.Mass[i];
                _nodeMass[nodeIndex[j]] += nodeMass;
                _nodeMomentum[nodeIndex[j]] += nodeMass * nodeVelocity;
            }
        }
    }

    /// <summary>
    /// Calculate velocity gradient of the grid
    /// </summary>
    public void CalculateVelocity() {
        for (int i = 0; i < _velocityX.Length; ++i) {
            /*
             * Get bottom Node velocity index
             */
            int index = i + i / Size.y;

            /*
             * Calculate velocity on the X staggered faces of the Grid
             */
            float mass = _nodeMass[index] + _nodeMass[index + 1];
            _velocityX[i] = (mass == 0f)
                ? 0f
                : (_nodeMomentum[index].x + _nodeMomentum[index + 1].x) / mass;
        }

        for (int i = 0; i < _velocityY.Length; ++i) {
            /*
             * Calculate velocity on the Y staggered faces of the Grid
             */
            float mass = _nodeMass[i] + _nodeMass[i + Size.y + 1];
            _velocityY[i] = (mass == 0f)
                ? 0f
                : (_nodeMomentum[i].y + _nodeMomentum[i + Size.y + 1].y) / mass;
        }
    }

    /// <summary>
    /// Apply external forces to the entire Grid such as Gravity
    /// </summary>
    /// <param name="acceleration">The acceleration generated by the applied force</param>
    /// <param name="dt">The time step of the simulation</param>
    public void ApplyExternalForces(float2 acceleration, float dt) {
        for (int i = 0; i < _velocityX.Length; ++i) {
            _velocityX[i] += acceleration.x * dt;
        }

        for (int i = 0; i < _velocityY.Length; ++i) {
            _velocityY[i] += acceleration.y * dt;
        }
    }

    /// <summary>
    /// Enforce Grid boundaries by applying zero velocity at the Grid borders
    /// </summary>
    public void EnforceBoundaries() {
        for (int i = 0; i < Size.y; ++i) {
            _velocityX[i] = 0f;
            _velocityX[i + Size.y * Size.x] = 0f;
        }
        for (int i = 0; i < Size.x; ++i) {
            _velocityY[i * Size.x] = 0f;
            _velocityY[(i + 1) * Size.x - 1] = 0f;
        }
    }

    /// <summary>
    /// Calculate Grid divergence used to solve the pressure problem
    /// </summary>
    public void CalculateDivergence() {
        for (int x = 0; x < Size.x; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int index = math.mad(x, Size.y, y);
                int left = index;
                int right = left + Size.y;
                int bottom = index + x;
                int top = bottom + 1;
                _divergence[index] = (_velocityX[right] - _velocityX[left]) +
                    (_velocityY[top] - _velocityY[bottom]);
            }
        }
    }
}
