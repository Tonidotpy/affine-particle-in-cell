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
    private NativeArray<float2> _momentum; // Momentum is located at each Grid Node

    public int2 Size { get { return _size - 2; } }  // Size attribute excludes the "ghost Cells" layer
    public float CellSize { get; }
    public float CellArea { get { return CellSize * CellSize; } }

    public NativeArray<float> Mass { get { return _mass; } }
    public NativeArray<float> Pressure { get; }
    public NativeArray<float> VelocityX { get; }
    public NativeArray<float> VelocityY { get; }
    public NativeArray<float2> Momentum { get { return _momentum; } }

    /// <summary>
    /// Get the density of a single Cell
    /// </summary>
    /// <para>
    /// If the given index is outside of the grid bounds the returned density
    /// is the density of the nearest Cell
    /// </para>
    /// <param name="index">The coordinates of the Cell</param>
    public float GetCellDensity(int2 index) {
        // Shift by one due to the ghost layer
        index = math.clamp(index + 1, int2.zero, _size);
        return _mass[index.x * _size.y + index.y] / CellArea;
    }

    /// <summary>
    /// Staggered Grid constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="size">The size of the grid (width, height)</param>
    /// <param name="allocator">The used memory allocator</param>
    public StaggeredGrid(int2 size, float cellSize, Allocator allocator) {
        // Add a layer of "ghost Cells" around the grid
        _size = size + 2;
        CellSize = cellSize;

        int area = _size.x * _size.y;
        int nodes = (Size.x + 1) * (Size.y + 1); // Momentum does not need a ghost layer
        int velocityXSize = (_size.x + 1) * _size.y;
        int velocityYSize = _size.x * (_size.y + 2);

        _mass = new NativeArray<float>(area, allocator);
        _momentum = new NativeArray<float2>(nodes, allocator);
        Pressure = new NativeArray<float>(area, allocator);
        VelocityX = new NativeArray<float>(velocityXSize, allocator);
        VelocityY = new NativeArray<float>(velocityYSize, allocator);
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (_mass.IsCreated) _mass.Dispose();
        if (_momentum.IsCreated) _momentum.Dispose();
        if (Pressure.IsCreated) Pressure.Dispose();
        if (VelocityX.IsCreated) VelocityX.Dispose();
        if (VelocityY.IsCreated) VelocityY.Dispose();
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
            long byteSize = _momentum.Length * (long)UnsafeUtility.SizeOf<float2>();
            UnsafeUtility.MemClear(_momentum.GetUnsafePtr(), byteSize);
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

                _momentum[nodeIndex[j]] += weights[j] * parcels.Mass[i] * nodeVelocity;
            }
        }
    }

    /// <summary>
    /// Calculate velocity gradient of the grid
    /// </summary>
    public void CalculateVelocity() { }
}
