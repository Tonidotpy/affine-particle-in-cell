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

    public NativeArray<float> Mass;
    public NativeArray<float> Pressure;
    public NativeArray<float> VelocityX;
    public NativeArray<float> VelocityY;

    /*
     * Auxiliary arrays used for momentum transfer
     * Momentum and mass are located at the Grid Nodes
     */
    public NativeArray<float2> NodeMomentum;
    public NativeArray<float> NodeMass;

    /*
     * Auxiliary array used to store divergence used to solve the pressure problem
     */
    public NativeArray<float> Divergence;

    public int2 Size { get { return _size - 2; } }  // Size attribute excludes the "ghost Cells" layer
    public float CellSize { get; }
    public float CellArea { get { return CellSize * CellSize; } }


    /// <summary>
    /// Get density of a single cell
    /// </summary>
    /// <para>
    /// Cell index is clamped inside the Grid bounds, ghost layer included
    /// </para>
    /// <param name="index">The index of the Grid Cell (x, y)</param>
    /// <returns>The Cell density</param>
    public float GetCellDensity(int2 index) {
        // Shift by one due to the ghost layer
        index = math.clamp(index + 1, int2.zero, _size);
        return Mass[math.mad(index.x, _size.y, index.y)] / CellArea;
    }

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
        Mass = new NativeArray<float>(area, allocator);
        Pressure = new NativeArray<float>(area, allocator);

        int velocityXSize = (Size.x + 1) * Size.y;
        int velocityYSize = Size.x * (Size.y + 1);
        VelocityX = new NativeArray<float>(velocityXSize, allocator);
        VelocityY = new NativeArray<float>(velocityYSize, allocator);

        int nodes = (Size.x + 1) * (Size.y + 1);
        NodeMomentum = new NativeArray<float2>(nodes, allocator);
        NodeMass = new NativeArray<float>(nodes, allocator);

        int boundedArea = Size.x * Size.y;
        Divergence = new NativeArray<float>(boundedArea, allocator);
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (Mass.IsCreated) Mass.Dispose();
        if (Pressure.IsCreated) Pressure.Dispose();
        if (VelocityX.IsCreated) VelocityX.Dispose();
        if (VelocityY.IsCreated) VelocityY.Dispose();

        if (NodeMomentum.IsCreated) NodeMomentum.Dispose();
        if (NodeMass.IsCreated) NodeMass.Dispose();
        if (Divergence.IsCreated) Divergence.Dispose();
    }

    /// <summary>
    /// Resets grid masses and velocity values
    /// </summary>
    public void Reset() { 
        unsafe {
            long byteSize = Mass.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(Mass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = NodeMomentum.Length * (long)UnsafeUtility.SizeOf<float2>();
            UnsafeUtility.MemClear(NodeMomentum.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = NodeMass.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(NodeMass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = VelocityX.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(VelocityX.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = VelocityY.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(VelocityY.GetUnsafePtr(), byteSize);
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
                Mass[cellIndex[j]] += weights[j] * parcels.Mass[i];
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
                NodeMass[nodeIndex[j]] += nodeMass;
                NodeMomentum[nodeIndex[j]] += nodeMass * nodeVelocity;
            }
        }
    }

    /// <summary>
    /// Calculate velocity gradient of the grid
    /// </summary>
    public void CalculateVelocity() {
        for (int i = 0; i < VelocityX.Length; ++i) {
            /*
             * Get bottom Node velocity index
             */
            int index = i + i / Size.y;

            /*
             * Calculate velocity on the X staggered faces of the Grid
             */
            float mass = NodeMass[index] + NodeMass[index + 1];
            VelocityX[i] = (mass == 0f)
                ? 0f
                : (NodeMomentum[index].x + NodeMomentum[index + 1].x) / mass;
        }

        for (int i = 0; i < VelocityY.Length; ++i) {
            /*
             * Calculate velocity on the Y staggered faces of the Grid
             */
            float mass = NodeMass[i] + NodeMass[i + Size.y + 1];
            VelocityY[i] = (mass == 0f)
                ? 0f
                : (NodeMomentum[i].y + NodeMomentum[i + Size.y + 1].y) / mass;
        }
    }

    /// <summary>
    /// Apply external forces to the entire Grid such as Gravity
    /// </summary>
    /// <param name="acceleration">The acceleration generated by the applied force</param>
    /// <param name="dt">The time step of the simulation</param>
    public void ApplyExternalForces(float2 acceleration, float dt) {
        for (int i = 0; i < VelocityX.Length; ++i) {
            VelocityX[i] += acceleration.x * dt;
        }

        for (int i = 0; i < VelocityY.Length; ++i) {
            VelocityY[i] += acceleration.y * dt;
        }
    }

    /// <summary>
    /// Enforce Grid boundaries by applying zero velocity at the Grid borders
    /// </summary>
    public void EnforceBoundaries() {
        for (int i = 0; i < Size.y; ++i) {
            VelocityX[i] = 0f;
            VelocityX[i + Size.y * Size.x] = 0f;
        }
        for (int i = 0; i < Size.x; ++i) {
            VelocityY[i * Size.x] = 0f;
            VelocityY[(i + 1) * Size.x - 1] = 0f;
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
                Divergence[index] = (VelocityX[right] - VelocityX[left]) +
                    (VelocityY[top] - VelocityY[bottom]);
            }
        }
    }

    /// <summary>
    /// Correct Grid velocities to achieve a divergent-free behavior
    /// </summary>
    /// <param name="dt">The time step of the simulation</param>
    public void CorrectVelocity(float dt) {
        for (int i = 0; i < VelocityX.Length; ++i) {
            int2 cellRight = new int2(i / Size.y, i % Size.y);
            int2 cellLeft = cellRight - new int2(1, 0);
            float density = (GetCellDensity(cellRight) + GetCellDensity(cellLeft)) * 0.5f;

            int right = math.mad(cellRight.x + 1, _size.y, cellRight.y + 1);
            int left = math.mad(cellLeft.x + 1, _size.y, cellLeft.y + 1);
            VelocityX[i] -= dt / density * (Pressure[right] - Pressure[left]);
        }

        for (int i = 0; i < VelocityY.Length; ++i) {
            int2 cellTop = new int2(i / (Size.y + 1), i % (Size.y + 1));
            int2 cellBottom = cellTop - new int2(0, 1);
            float density = (GetCellDensity(cellTop) + GetCellDensity(cellBottom)) * 0.5f;

            int top = math.mad(cellTop.x + 1, _size.y, cellTop.y + 1);
            int bottom = math.mad(cellBottom.x + 1, _size.y, cellBottom.y + 1);
            VelocityY[i] -= dt / density * (Pressure[top] - Pressure[bottom]);
        } 
    }
}
