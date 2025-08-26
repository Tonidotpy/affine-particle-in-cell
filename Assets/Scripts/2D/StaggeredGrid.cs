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
    public NativeArray<CellType> Type;
    public NativeArray<float> Mass;
    public NativeArray<float> Pressure;
    public NativeArray<float> VelocityX;
    public NativeArray<float> VelocityY;

    /*
     * Auxiliary arrays used for momentum transfer
     * Momentum and mass are located at the Grid edges
     */
    public NativeArray<float> MassX;
    public NativeArray<float> MassY;
    public NativeArray<float> MomentumX;
    public NativeArray<float> MomentumY;

    /*
     * Auxiliary array used to store divergence used to solve the pressure problem
     */
    public NativeArray<float> Divergence;

    public float FluidDensity { get; }
    public float AirDensity { get; }
    public int2 Size { get; }
    public int2 BoundedSize { get { return Size - 2; } }
    public int Area { get { return Size.x * Size.y; } }
    public int BoundedArea { get { return BoundedSize.x * BoundedSize.y; } }
    public float CellSize { get; }
    public float CellArea { get { return CellSize * CellSize; } }

    /// <summary>
    /// Staggered Grid constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="size">The size of the grid (width, height)</param>
    /// <param name="cellSize">Size of a single Cell of the Grid</param>
    /// <param name="fluidDensity">The fluid density</param>
    /// <param name="airDensity">The air density</param>
    /// <param name="allocator">The used memory allocator</param>
    public StaggeredGrid(int2 size, float cellSize, float fluidDensity, float airDensity, Allocator allocator) {
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
        FluidDensity = fluidDensity;
        AirDensity = airDensity;

        Mass = new NativeArray<float>(Area, allocator);

        int extendedArea = (Size.x + 1) * (Size.y + 1);
        Type = new NativeArray<CellType>(extendedArea, allocator);
        Pressure = new NativeArray<float>(extendedArea, allocator);

        int edgeCountX = (Size.x + 1) * Size.y;
        int edgeCountY = Size.x * (Size.y + 1);
        VelocityX = new NativeArray<float>(edgeCountX, allocator);
        VelocityY = new NativeArray<float>(edgeCountY, allocator);
        MassX = new NativeArray<float>(edgeCountX, allocator);
        MassY = new NativeArray<float>(edgeCountY, allocator);
        MomentumX = new NativeArray<float>(edgeCountX, allocator);
        MomentumY = new NativeArray<float>(edgeCountY, allocator);

        /* Divergence does not need a ghost layer for the calculation */
        Divergence = new NativeArray<float>(BoundedArea, allocator);

        /* Set all outer cells type to solid */
        for (int x = 1; x < Size.x - 1; ++x) {
            int bottom = math.mad(x + 1, Size.y + 1, 1);
            int top = math.mad(x + 1, Size.y + 1, Size.y);
            Type[bottom] = Type[top] = CellType.Solid;
        }
        for (int y = 1; y < Size.y - 1; ++y) {
            int left = math.mad(1, Size.y + 1, y + 1);
            int right = math.mad(Size.x, Size.y + 1, y + 1);
            Type[left] = Type[right] = CellType.Solid;
        }    
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (Type.IsCreated) Type.Dispose();
        if (Mass.IsCreated) Mass.Dispose();
        if (Pressure.IsCreated) Pressure.Dispose();
        if (VelocityX.IsCreated) VelocityX.Dispose();
        if (VelocityY.IsCreated) VelocityY.Dispose();
        if (MassX.IsCreated) MassX.Dispose();
        if (MassY.IsCreated) MassY.Dispose();
        if (MomentumX.IsCreated) MomentumX.Dispose();
        if (MomentumY.IsCreated) MomentumY.Dispose();
        if (Divergence.IsCreated) Divergence.Dispose();
    }

    /// <summary>
    /// Resets grid mass and velocity values
    /// </summary>
    public void Reset() { 
        for (int x = 1; x < Size.x - 1; ++x) {
            for (int y = 1; y < Size.y - 1; ++y) {
                int i = math.mad(x + 1, Size.y + 1, y + 1);
                Type[i] = CellType.Air;
            }
        }
        unsafe {
            long byteSize = Mass.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(Mass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = MassX.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(MassX.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = MassY.Length * (long)UnsafeUtility.SizeOf<float>();
            UnsafeUtility.MemClear(MassY.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = MomentumX.Length * (long)UnsafeUtility.SizeOf<float2>();
            UnsafeUtility.MemClear(MomentumX.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = MomentumY.Length * (long)UnsafeUtility.SizeOf<float2>();
            UnsafeUtility.MemClear(MomentumY.GetUnsafePtr(), byteSize);
        }
        // unsafe {
        //     long byteSize = VelocityX.Length * (long)UnsafeUtility.SizeOf<float>();
        //     UnsafeUtility.MemClear(VelocityX.GetUnsafePtr(), byteSize);
        // }
        // unsafe {
        //     long byteSize = VelocityY.Length * (long)UnsafeUtility.SizeOf<float>();
        //     UnsafeUtility.MemClear(VelocityY.GetUnsafePtr(), byteSize);
        // }
    }

    /// <summary>
    /// Transfer mass to the grid
    /// </summary>
    /// <param name="parcels">The parcel which mass need to be transfered</param>
    public void TransferMass(Parcels parcels) {
        for (int i = 0; i < parcels.Count; ++i) {
            /* Update Cell type */
            int2 cellIndex = (int2)math.floor(parcels.Position[i] / CellSize + 1f);
            int j = math.mad(cellIndex.x + 1, Size.y + 1, cellIndex.y + 1);
            Type[j] = CellType.Fluid;

            /*
             * Calculate the index of the Bottom-Left Cell inside the 2x2 block
             * that surround the Parcel
             * Shift by one due to the ghost layer
             */
            float2 staggeredParcel = parcels.Position[i] / CellSize + 0.5f;
            int2 bottomLeftCell = (int2)math.floor(staggeredParcel);
            
            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 fractionalPosition = staggeredParcel - bottomLeftCell;

            /*
             * Distribute mass over the Grid Cells via bilinear interpolation
             * based on the position of the Parcel
             */
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int index = math.mad(bottomLeftCell.x + x, Size.y, bottomLeftCell.y + y);
                    float weight = 1;
                    weight *= (x == 0) ? (1f - fractionalPosition.x) : fractionalPosition.x;
                    weight *= (y == 0) ? (1f - fractionalPosition.y) : fractionalPosition.y;
                    Mass[index] += weight * parcels.Mass[i];
                }
            }
        }
    }

    /// <summary>
    /// Transfer momentum to the grid
    /// </summary>
    /// <param name="parcels">The parcel which mass need to be transfered</param>
    public void TransferMomentum(Parcels parcels) {
        // TODO: Store temporarely momentum inside velocity vector to save memory
        for (int i = 0; i < parcels.Count; ++i) {
            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the X axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionX = parcels.Position[i] / CellSize + new float2(1f, 0.5f);
            int2 bottomLeftNodeX = (int2)math.floor(parcelPositionX);
            float2 fractionalPositionX = parcelPositionX - bottomLeftNodeX;

            /*
             * Calculate mass and momentum on the X axis of the staggered Grid
             */
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node position and index */
                    int2 nodeIndexX = bottomLeftNodeX + new int2(x, y);
                    float2 nodePositionX = (float2)nodeIndexX * CellSize;
                    int index = math.mad(nodeIndexX.x, Size.y, nodeIndexX.y);

                    /*
                     * Calculate weight based on Parcel position inside the
                     * staggered Cell
                     */
                    float weight = 1;
                    weight *= (x == 0) ? (1f - fractionalPositionX.x) : fractionalPositionX.x;
                    weight *= (y == 0) ? (1f - fractionalPositionX.y) : fractionalPositionX.y;

                    /*
                     * Calculate mass and momentum on the edges of the Cell
                     */
                    float mass = parcels.Mass[i] * weight;
                    float dv = math.mul(
                        parcels.AffineState[i][0],
                        nodePositionX - parcels.Position[i]
                    );
                    MassX[index] += mass;
                    MomentumX[index] += mass * (parcels.Velocity[i].x + dv);
                }
            }

            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the Y axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionY = parcels.Position[i] / CellSize + new float2(0.5f, 1f);
            int2 bottomLeftNodeY = (int2)math.floor(parcelPositionY);
            float2 fractionalPositionY = parcelPositionY - bottomLeftNodeY;

            /*
             * Calculate mass and momentum on the Y axis of the staggered Grid
             */
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node position and index */
                    int2 nodeIndexY = bottomLeftNodeY + new int2(x, y);
                    float2 nodePositionY = (float2)nodeIndexY * CellSize;
                    int index = math.mad(nodeIndexY.x, Size.y + 1, nodeIndexY.y);

                    /*
                     * Calculate weight based on Parcel position inside the
                     * staggered Cell
                     */
                    float weight = 1;
                    weight *= (x == 0) ? (1f - fractionalPositionY.x) : fractionalPositionY.x;
                    weight *= (y == 0) ? (1f - fractionalPositionY.y) : fractionalPositionY.y;

                    /*
                     * Calculate mass and momentum on the edges of the Cell
                     */
                    float mass = parcels.Mass[i] * weight;
                    float dv = math.mul(
                        parcels.AffineState[i][1],
                        nodePositionY - parcels.Position[i]
                    );
                    MassY[index] += mass;
                    MomentumY[index] += mass * (parcels.Velocity[i].y + dv);
                }
            }
        }
    }

    /// <summary>
    /// Calculate velocity gradient of the grid
    /// </summary>
    public void CalculateVelocity() {
        /*
         * Calculate staggered X velocity
         */
        for (int x = 0; x < Size.x + 1; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int i = math.mad(x, Size.y, y);
                VelocityX[i] = (MassX[i] == 0f) ? 0f : MomentumX[i] / MassX[i];
            }
        }

        /*
         * Calculate staggered Y velocity
         */
        for (int x = 0; x < Size.x; ++x) {
            for (int y = 0; y < Size.y + 1; ++y) {
                int i = math.mad(x, Size.y + 1, y);
                VelocityY[i] = (MassY[i] == 0f) ? 0f : MomentumY[i] / MassY[i];
            }
        }
    }

    /// <summary>
    /// Apply external forces to the entire Grid such as Gravity
    /// </summary>
    /// <param name="acceleration">The acceleration generated by the applied force</param>
    /// <param name="dt">The time step of the simulation</param>
    public void ApplyExternalForces(float2 acceleration, float dt) {
        /*
         * Apply force on staggered X velocity
         */
        for (int x = 0; x < Size.x + 1; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int i = math.mad(x, Size.y, y);
                VelocityX[i] += acceleration.x * dt;
            }
        }
        /*
         * Apply force on staggered Y velocity
         */
        for (int x = 0; x < Size.x; ++x) {
            for (int y = 0; y < Size.y + 1; ++y) {
                int i = math.mad(x, Size.y + 1, y);
                VelocityY[i] += acceleration.y * dt;
            }
        }
    }

    /// <summary>
    /// Enforce Grid boundaries by applying zero velocity at the Grid borders
    /// </summary>
    public void EnforceBoundaries() {
        /*
         * Reset X velocity along the vertical borders of the Grid
         */
        for (int y = 0; y < Size.y; ++y) {
            int left = math.mad(0, Size.y, y);
            int right = math.mad(Size.x, Size.y, y);
            int boundedLeft = math.mad(1, Size.y, y);
            int boundedRight = math.mad(Size.x - 1, Size.y, y);
            VelocityX[left] = VelocityX[right] = 0f;
            VelocityX[boundedLeft] = VelocityX[boundedRight] = 0f;
        }
        /*
         * Reset Y velocity along the horizontal borders of the Grid
         */
        for (int x = 0; x < Size.x; ++x) {
            int bottom = math.mad(x, Size.y + 1, 0);
            int top = math.mad(x, Size.y + 1, Size.y);
            int boundedBottom = math.mad(x, Size.y + 1, 1);
            int boundedTop = math.mad(x, Size.y + 1, Size.y - 1);
            VelocityY[bottom] = VelocityY[top] = 0f;
            VelocityY[boundedBottom] = VelocityY[boundedTop] = 0f;
        }
        
        /*
         * Reset X velocity on the top and bottom ghost layers of the Grid
         */
        for (int x = 0; x < Size.x + 1; ++x) {
            int bottom = math.mad(x, Size.y, 0);
            int top = math.mad(x, Size.y, Size.y - 1);
            VelocityX[bottom] = VelocityX[top] = 0f;
        }

        /*
         * Reset Y velocity on the right and left ghost layers of the Grid
         */
        for (int y = 0; y < Size.y + 1; ++y) {
            int left = math.mad(0, Size.y + 1, y);
            int right = math.mad(Size.x - 1, Size.y + 1, y);
            VelocityY[left] = VelocityY[right] = 0f;
        }
    }

    /// <summary>
    /// Calculate Grid divergence used to solve the pressure problem
    /// </summary>
    public void CalculateDivergence() {
        for (int x = 0; x < BoundedSize.x; ++x) {
            for (int y = 0; y < BoundedSize.y; ++y) {
                /*
                 * Calculate indices of the four edges of the current Cell
                 */
                int i = math.mad(x, BoundedSize.y, y);
                int left = math.mad(x + 1, Size.y, y + 1);
                int right = math.mad(x + 2, Size.y, y + 1);
                int bottom = math.mad(x + 1, Size.y + 1, y + 1);
                int top = math.mad(x + 1, Size.y + 1, y + 2);

                /*
                 * Calculate Cell divergence based on staggered velocities
                 */
                Divergence[i] = (VelocityX[right] - VelocityX[left]) +
                    (VelocityY[top] - VelocityY[bottom]);
                Divergence[i] /= CellSize;
            }
        }
    }

    /// <summary>
    /// Correct Grid velocities to achieve a divergent-free behavior
    /// </summary>
    /// <param name="dt">The time step of the simulation</param>
    public void CorrectVelocity(float dt) {
        /*
         * Correct X velocity based on pressure gradient of the adjacent Cells
         * The first and last columns are skipped since the out of bounds velocity
         * does not need to be corrected
         */
        for (int x = 1; x < Size.x; ++x) {
            for (int y = 0; y < Size.y; ++y) {
                int r = math.mad(x + 1, Size.y + 1, y + 1);
                int l = math.mad(x, Size.y + 1, y + 1);
                if (Type[r] != CellType.Fluid && Type[l] != CellType.Fluid)
                    continue;

                /*
                 * Calculate indices of the Cells touching the current edge
                 */
                int i = math.mad(x, Size.y, y);
                int right = math.mad(x + 1, Size.y + 1, y + 1);
                int left = math.mad(x, Size.y + 1, y + 1);

                float density = FluidDensity;
                
                /*
                 * Correct the edge velocity based on pressure gradient and edge
                 * density
                 */
                float dp = (Pressure[right] - Pressure[left]) / CellSize;
                VelocityX[i] -= dt / density * dp;
            }
        }

        /*
         * Correct X velocity based on pressure gradient of the adjacent Cells
         */
        for (int x = 0; x < Size.x; ++x) {
            for (int y = 1; y < Size.y; ++y) {
                int t = math.mad(x + 1, Size.y + 1, y + 1);
                int b = math.mad(x + 1, Size.y + 1, y);
                if (Type[t] != CellType.Fluid && Type[b] != CellType.Fluid)
                    continue;

                /*
                 * Calculate indices of the Cells touching the current edge
                 */
                int i = math.mad(x, Size.y + 1, y);
                int top = math.mad(x + 1, Size.y + 1, y + 1);
                int bottom = math.mad(x + 1, Size.y + 1, y);

                float density = FluidDensity;

                /*
                 * Correct the edge velocity based on pressure gradient and edge
                 * density
                 */
                float dp = (Pressure[top] - Pressure[bottom]) / CellSize;
                VelocityY[i] -= dt / density * dp;
            }
        } 
    }
}
