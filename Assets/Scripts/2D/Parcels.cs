using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Fluid macro-particles called Parcels
/// </summary>
/// <para>
/// This class represent a group of Parcels, each Parcel is like a "macro-particle"
/// which theoretically contains particles with similar behavior (velocity for
/// example).
/// The model variables of a Parcel are:
/// <list type="bullet">
///     <item>
///         <term>Mass</term>
///         <description>
///         Mass of the Parcel
///         </description>
///     </item>
///     <item>
///         <term>Position</term>
///         <description>
///         Position of the Parcel in the space
///         </description>
///     </item>
///     <item>
///         <term>Velocity</term>
///         <description>
///         Velocity of the Parcel
///         </description>
///     </item>
///     <item>
///         <term>AffineState</term>
///         <description>
///         Matrix containing additional information used to preserve angular
///         momentum of the fluid
///         </description>
///     </item>
/// </list>
/// </para>
public class Parcels {
    public NativeArray<float2> Position;
    public NativeArray<float2> Velocity;
    public NativeArray<float> Mass;
    public NativeArray<float2x2> AffineState;

    public int Count { get; }

    /// <summary>
    /// Parcels constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="count">The total number of particles</param>
    /// <param name="allocator">The used memory allocator</param>
    public Parcels(int count, Allocator allocator) {
        Count = count;

        Position = new NativeArray<float2>(count, allocator);
        Velocity = new NativeArray<float2>(count, allocator);
        Mass = new NativeArray<float>(count, allocator);
        AffineState = new NativeArray<float2x2>(count, allocator);

        // TODO: Remove, for test purposes only
        UnityEngine.Random.InitState(0);
        for (int i = 0; i < count; ++i) {
            // Position[i] = new float2(
            //     UnityEngine.Random.Range(0.2f, 7.8f),
            //     UnityEngine.Random.Range(0.2f, 5.8f)
            // );
            // Velocity[i] = new float2(
            //     UnityEngine.Random.Range(-1f, 1f),
            //     UnityEngine.Random.Range(-1f, 1f)
            // );
            Mass[i] = 1f;
        }
        Position[0] = new float2(3, 3);
        // Position[1] = new float2(7, 3);
        Velocity[0] = new float2(0, 1);
        // Velocity[1] = new float2(1, 0);
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be calluied at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (Position.IsCreated) Position.Dispose();
        if (Velocity.IsCreated) Velocity.Dispose();
        if (Mass.IsCreated) Mass.Dispose();
        if (AffineState.IsCreated) AffineState.Dispose();
    }

    /// <summary>
    /// Update Parcels velocity
    /// </summary>
    public void UpdateVelocity(StaggeredGrid grid) {
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 baseParcel = Position[i].x / grid.CellSize - 0.5f;
            int2 index = (int2)math.floor(baseParcel);

            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 t = baseParcel - index;

            // TODO: Find velocity indices
            float2 u00 = new float2(grid.VelocityX[0], grid.VelocityY[0]);
            float2 u01 = new float2(grid.VelocityX[0], grid.VelocityY[0]);
            float2 u10 = new float2(grid.VelocityX[0], grid.VelocityY[0]);
            float2 u11 = new float2(grid.VelocityX[0], grid.VelocityY[0]);
            
            /*
             * Update velocity using bilinear interpolation base on Parcel
             * fractional's position
             */
            Velocity[i] = math.lerp(
                math.lerp(u00, u10, t.xx),
                math.lerp(u01, u11, t.xx),
                t.yy
            );
        }
    }
}
