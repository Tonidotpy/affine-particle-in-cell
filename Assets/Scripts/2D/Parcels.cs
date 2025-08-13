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
    private NativeArray<float2> _position;
    private NativeArray<float2> _velocity;
    private NativeArray<float> _mass;
    private NativeArray<float2x2> _affineState;

    public int Count { get; }
    public NativeArray<float2> Position { get { return _position; } }
    public NativeArray<float2> Velocity { get { return _velocity; } }
    public NativeArray<float> Mass { get { return _mass; } }
    public NativeArray<float2x2> AffineState { get { return _affineState; } }

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

        _position = new NativeArray<float2>(count, allocator);
        _velocity = new NativeArray<float2>(count, allocator);
        _mass = new NativeArray<float>(count, allocator);
        _affineState = new NativeArray<float2x2>(count, allocator);

        // TODO: Remove, for test purposes only
        for (int i = 0; i < count; ++i) {
            _position[i] = new float2(
                UnityEngine.Random.Range(0.2f, 7.8f),
                UnityEngine.Random.Range(0.2f, 5.8f)
            );
            _velocity[i] = new float2(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            );
            _mass[i] = 1f;
        }
    }
    
    /// <summary>
    /// Releases unmanaged memory
    /// This method MUST be called at the end of the program to avoid memory leaks
    /// </summary>
    public void Dispose() {
        if (_position.IsCreated) _position.Dispose();
        if (_velocity.IsCreated) _velocity.Dispose();
        if (_mass.IsCreated) _mass.Dispose();
        if (_affineState.IsCreated) _affineState.Dispose();
    }
}
