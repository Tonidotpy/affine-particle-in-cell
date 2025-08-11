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

    public int2 Size { get { return _size - 2; } }
    public float CellSize { get; }
    public float CellArea { get { return CellSize * CellSize; } }

    public NativeArray<float> Mass { get { return _mass; } }
    public NativeArray<float> Pressure { get; }
    public NativeArray<float> VelocityX { get; }
    public NativeArray<float> VelocityY { get; }

    /// <summary>
    /// Get the density of a single Cell
    /// </summary>
    /// <para>
    /// If the given index is outside of the grid bounds the returned density
    /// is the density of the nearest Cell
    /// </para>
    /// <param name="index">The coordinates of the Cell</param>
    public float GetCellDensity(int2 index) {
        index = math.clamp(index, int2.zero, _size);
        return _mass[(index.x + 1) * _size.y + (index.y + 1)] / CellArea;
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
        _size = size + 2;
        CellSize = cellSize;

        // Add a layer of "ghost Cells" around the grid
        int area = _size.x * _size.y;
        int velocityXSize = (_size.x + 1) * _size.y;
        int velocityYSize = _size.x * (_size.y + 2);

        _mass = new NativeArray<float>(area, allocator);
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
        if (Pressure.IsCreated) Pressure.Dispose();
        if (VelocityX.IsCreated) VelocityX.Dispose();
        if (VelocityY.IsCreated) VelocityY.Dispose();
    }

    /// <summary>
    /// Resets grid masses and velocity values
    /// </summary>
    public void Reset() { 
        unsafe {
            long byteSize = _mass.Length * (long)UnsafeUtility.SizeOf<int>();
            UnsafeUtility.MemClear(_mass.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = VelocityX.Length * (long)UnsafeUtility.SizeOf<int>();
            UnsafeUtility.MemClear(VelocityX.GetUnsafePtr(), byteSize);
        }
        unsafe {
            long byteSize = VelocityY.Length * (long)UnsafeUtility.SizeOf<int>();
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
             */
            int2 index = (int2)math.floor(baseParcel);
            
            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 t = baseParcel - index;

            /*
             * Distribute mass over the Grid Cells via bilinear interpolation
             * based on the position of the Parcel
             */
            int[] clampedIndex = new int[] {
                (index.x + 1) * _size.y + (index.y + 1), // Bottom-Left
                (index.x + 2) * _size.y + (index.y + 1), // Bottom-Right
                (index.x + 1) * _size.y + (index.y + 2), // Top-Left
                (index.x + 2) * _size.y + (index.y + 2), // Top-Right
            };
            _mass[clampedIndex[0]] += (1 - t.x) * (1 - t.y) * parcels.Mass[i];
            _mass[clampedIndex[1]] +=      t.x  * (1 - t.y) * parcels.Mass[i];
            _mass[clampedIndex[2]] += (1 - t.x) *      t.y  * parcels.Mass[i];
            _mass[clampedIndex[3]] +=      t.x  *      t.y  * parcels.Mass[i];
        }
    }

    /// <summary>
    /// Transfer momentum to the grid
    /// </summary>
    public void TransferMomentum() { }

    /// <summary>
    /// Calculate velocity gradient of the grid
    /// </summary>
    public void CalculateVelocity() { }
}
