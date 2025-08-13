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
            Position[i] = new float2(
                UnityEngine.Random.Range(0.2f, 7.8f),
                UnityEngine.Random.Range(0.2f, 5.8f)
            );
            Velocity[i] = new float2(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            );
            Mass[i] = 1f;
        }
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (Position.IsCreated) Position.Dispose();
        if (Velocity.IsCreated) Velocity.Dispose();
        if (Mass.IsCreated) Mass.Dispose();
        if (AffineState.IsCreated) AffineState.Dispose();
    }
}
