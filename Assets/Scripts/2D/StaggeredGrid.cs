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

    public float Density { get; }
    public int2 Size { get; }
    public int2 BoundedSize { get { return Size - 2; } }
    public int Area { get { return Size.x * Size.y; } }
    public int BoundedArea { get { return BoundedSize.x * BoundedSize.y; } }
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
    public float GetCellDensity(int2 cellIndex) {
        // Shift by one due to the ghost layer
        cellIndex = math.clamp(cellIndex + 1, int2.zero, Size);
        int index = math.mad(cellIndex.x, Size.y, cellIndex.y);
        return Mass[index] / CellArea;
    }

    /// <summary>
    /// Staggered Grid constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="size">The size of the grid (width, height)</param>
    /// <param name="cellSize">Size of a single Cell of the Grid</param>
    /// <param name="density">The fluid consant density</param>
    /// <param name="allocator">The used memory allocator</param>
    public StaggeredGrid(int2 size, float cellSize, float density, Allocator allocator) {
        /*
         * To simply calculations and avoid conditional expresions for bound
         * checking a "ghost layer" is added around the Grid
         */
        Size = size + 2;
        CellSize = cellSize;

        /*
         * Set costant density value to make pressure able to propagate through
         * the air
         */
        Density = density;

        Mass = new NativeArray<float>(Area, allocator);
        Pressure = new NativeArray<float>(Area, allocator);

        /*
         * For the velocity the ghost layer is added only to the Y axis for the
         * X velocity and X axis for the Y velocity since in the other direction
         * they are not needed
         */
        int velocityXSize = (Size.x - 1) * Size.y;
        int velocityYSize = Size.x * (Size.y - 1);
        VelocityX = new NativeArray<float>(velocityXSize, allocator);
        VelocityY = new NativeArray<float>(velocityYSize, allocator);

        int nodes = (Size.x + 1) * (Size.y + 1);
        NodeMomentum = new NativeArray<float2>(nodes, allocator);
        NodeMass = new NativeArray<float>(nodes, allocator);

        /* Divergence does not need a ghost layer for the calculation */
        Divergence = new NativeArray<float>(BoundedArea, allocator);
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
                math.mad(index.x    , Size.y, index.y    ), // Bottom-Left
                math.mad(index.x + 1, Size.y, index.y    ), // Bottom-Right
                math.mad(index.x    , Size.y, index.y + 1), // Top-Left
                math.mad(index.x + 1, Size.y, index.y + 1)  // Top-Right
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
            int2 index = (int2)math.floor(rescaledParcelPosition + 1);

            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 t = rescaledParcelPosition - (index - 1);

            /*
             * Distribute momentum over the Grid Cells via bilinear interpolation
             * based on the position of the Parcel
             */
            int2[] rescaledNodePosition = new int2[] {
                (index -             1 ), // Bottom-Left
                (index - new int2(0, 1)), // Bottom-Right
                (index - new int2(1, 0)), // Top-Left
                (index                 )  // Top-Right
            };
            int[] nodeIndex = new int[] {
                math.mad(index.x    , Size.y + 1, index.y    ), // Bottom-Left
                math.mad(index.x + 1, Size.y + 1, index.y    ), // Bottom-Right
                math.mad(index.x    , Size.y + 1, index.y + 1), // Top-Left
                math.mad(index.x + 1, Size.y + 1, index.y + 1)  // Top-Right
            };
            float[] weights = new float[] {
                (1 - t.x) * (1 - t.y), // Bottom-Left
                     t.x  * (1 - t.y), // Bottom-Right
                (1 - t.x) *      t.y , // Top-Left
                     t.x  *      t.y   // Top-Right
            };
            
            /*
             * Add weighted momentum to Grid Nodes
             */
            for (int j = 0; j < nodeIndex.Length; ++j) {
                // Calculate velocity at node position
                float2 nodeVelocity = parcels.Velocity[i] +
                    math.mul(
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
        for (int x = 1; x < Size.x - 1; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int i = math.mad(x - 1, Size.y, y);
                int bottomNode = math.mad(x, Size.y + 1, y);
                int topNode = bottomNode + 1;

                /*
                 * Calculate velocity on the X staggered faces of the Grid
                 */
                float mass = NodeMass[bottomNode] + NodeMass[topNode];
                VelocityX[i] = (mass == 0f)
                    ? 0f
                    : (NodeMomentum[bottomNode].x + NodeMomentum[topNode].x) / mass;
            }
        }

        for (int x = 0; x < Size.x; ++x) {
            for (int y = 1; y < Size.y - 1; ++y) {
                int i = math.mad(x, BoundedSize.y + 1, y - 1);
                int leftNode = math.mad(x, Size.y + 1, y);
                int rightNode = leftNode + Size.y + 1;

                /*
                 * Calculate velocity on the Y staggered faces of the Grid
                 */
                float mass = NodeMass[leftNode] + NodeMass[rightNode];
                VelocityY[i] = (mass == 0f)
                    ? 0f
                    : (NodeMomentum[leftNode].y + NodeMomentum[rightNode].y) / mass;
            }
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
        for (int y = 0; y < Size.y; ++y) {
            int left = y;
            int right = math.mad(BoundedSize.x, Size.y, y);
            VelocityX[left] = VelocityX[right] = 0f;
        }
        for (int x = 0; x < Size.x; ++x) {
            int bottom = math.mad(x, BoundedSize.y + 1, 0);
            int top = math.mad(x, BoundedSize.y + 1, BoundedSize.y);
            VelocityY[bottom] = VelocityY[top] = 0f;
        }
    }

    /// <summary>
    /// Calculate Grid divergence used to solve the pressure problem
    /// </summary>
    public void CalculateDivergence() {
        for (int x = 0; x < BoundedSize.x; ++x) {
            for (int y = 0; y < BoundedSize.y; ++y) {
                int index = math.mad(x, BoundedSize.y, y);
                int left = math.mad(x, Size.y, y + 1);
                int right = left + Size.y;
                int bottom = math.mad(x + 1, BoundedSize.y + 1, y);
                int top = bottom + 1;
                Divergence[index] = (VelocityX[right] - VelocityX[left]) +
                    (VelocityY[top] - VelocityY[bottom]);
                Divergence[index] /= CellSize;
            }
        }
    }

    /// <summary>
    /// Correct Grid velocities to achieve a divergent-free behavior
    /// </summary>
    /// <param name="dt">The time step of the simulation</param>
    public void CorrectVelocity(float dt) {
        for (int x = 1; x < Size.x - 1; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int i = math.mad(x - 1, Size.y, y);
                int right = math.mad(x, Size.y, y);
                int left = math.mad(x - 1, Size.y, y);
                float edgeDensity = Density * CellSize;
                if (edgeDensity != 0)
                    VelocityX[i] -= dt / edgeDensity * (Pressure[right] - Pressure[left]);
            }
        }

        for (int x = 0; x < Size.x; ++x) {
            for (int y = 1; y < Size.y - 1; ++y) {
                int i = math.mad(x, BoundedSize.y + 1, y - 1);
                int top = math.mad(x, Size.y, y);
                int bottom = math.mad(x, Size.y, y - 1);
                float edgeDensity = Density * CellSize;
                if (edgeDensity != 0)
                    VelocityY[i] -= dt / edgeDensity * (Pressure[top] - Pressure[bottom]);
            }
        } 
    }
}
