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
    public float Friction { get; }

    /// <summary>
    /// Parcels constructor
    /// </summary>
    /// <para>
    /// It allocates unmanaged memory
    /// </para>
    /// <param name="count">The total number of particles</param>
    /// <param name="friction">
    /// Friction used to reduce velocity of Parcels colliding with the Grid
    /// bounds. It is a number from 0 to 1.
    /// </param>
    /// <param name="allocator">The used memory allocator</param>
    public Parcels(int count, float friction, Allocator allocator) {
        Count = count;
        Friction = friction;

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
            // int half = count / 2;
            // float x = (i < half) ? 1f : 3.5f;
            // Position[i] = new float2(x + 0.5f, 3 + (i % half) * 0.2f);
            // Velocity[i] = new float2(1, 0);
            Mass[i] = 1f;
        }
        Position[0] = new float2(3, 3);
        Position[1] = new float2(7, 7);
        Position[2] = new float2(11, 11);

        Velocity[0] = new float2(1, 0);
        Velocity[1] = new float2(0, 1);
        Velocity[2] = new float2(1, 0);
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
    /// <param name="grid">The staggered Grid to take the velocity information from</param>
    public void UpdateVelocity(StaggeredGrid grid) {
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the X axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionX = Position[i] / grid.CellSize + new float2(1f, 0.5f);
            int2 bottomLeftNodeX = (int2)math.floor(parcelPositionX);
            float2 fractionalPositionX = parcelPositionX - bottomLeftNodeX;
            float2 velocity = float2.zero;

            /*
             * Calculate velocity on the X axis of the staggered Grid
             */
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node index */
                    int2 nodeIndexX = bottomLeftNodeX + new int2(x, y);
                    int index = math.mad(nodeIndexX.x, grid.Size.y, nodeIndexX.y);

                    /*
                     * Calculate weight based on Parcel position inside the
                     * staggered Cell
                     */
                    float weight = 1;
                    weight *= (x == 0) ? (1f - fractionalPositionX.x) : fractionalPositionX.x;
                    weight *= (y == 0) ? (1f - fractionalPositionX.y) : fractionalPositionX.y;

                    /* Calculate X component of the Parcel velocity */
                    velocity.x += weight * grid.VelocityX[index];
                }
            }

            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the Y axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionY = Position[i] / grid.CellSize + new float2(0.5f, 1f);
            int2 bottomLeftNodeY = (int2)math.floor(parcelPositionY);
            float2 fractionalPositionY = parcelPositionY - bottomLeftNodeY;

            /*
             * Calculate velocity on the Y axis of the staggered Grid
             */
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node index */
                    int2 nodeIndexY = bottomLeftNodeY + new int2(x, y);
                    int index = math.mad(nodeIndexY.x, grid.Size.y + 1, nodeIndexY.y);

                    /*
                     * Calculate weight based on Parcel position inside the
                     * staggered Cell
                     */
                    float weight = 1;
                    weight *= (x == 0) ? (1f - fractionalPositionY.x) : fractionalPositionY.x;
                    weight *= (y == 0) ? (1f - fractionalPositionY.y) : fractionalPositionY.y;

                    /* Calculate Y component of the Parcel velocity */
                    velocity.y += weight * grid.VelocityY[index];
                }
            }

            Velocity[i] = velocity;
        }
    }

    /// <summary>
    /// Update Parcels affine state matrix
    /// </summary>
    /// <param name="grid">The staggered Grid to take the velocity information from</param>
    public void UpdateAffineState(StaggeredGrid grid) {
        // TODO: Fix affine update
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the X axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionX = Position[i] / grid.CellSize + new float2(1f, 0.5f);
            int2 bottomLeftNodeX = (int2)math.floor(parcelPositionX);
            float2 fractionalPositionX = parcelPositionX - bottomLeftNodeX;

            /*
             * Calculate affine vector on the X axis of the staggered Grid
             */
            float2 cx = new float2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node index */
                    int2 nodeIndexX = bottomLeftNodeX + new int2(x, y);
                    int index = math.mad(nodeIndexX.x, grid.Size.y, nodeIndexX.y);

                    /*
                     * Calculate partial derivative of weight based on Parcel
                     * position inside the staggered Cell
                     */
                    float2 dw = new float2(
                        (y == 0) ? (1f - fractionalPositionX.y) : fractionalPositionX.y,
                        (x == 0) ? (1f - fractionalPositionX.x) : fractionalPositionX.x
                    );
                    if (x == 0) dw.x *= -1f;
                    if (y == 0) dw.y *= -1f;
                    dw /= grid.CellSize;
                    
                    cx += dw * grid.VelocityX[index];
                }
            }

            /*
             * Calculate Parcel position and Bottom-Left Node of the Staggered
             * Grid for the Y axis
             * Offset by one is added due to the ghost layer
             */
            float2 parcelPositionY = Position[i] / grid.CellSize + new float2(0.5f, 1f);
            int2 bottomLeftNodeY = (int2)math.floor(parcelPositionY);
            float2 fractionalPositionY = parcelPositionY - bottomLeftNodeY;

            /*
             * Calculate affine vector on the Y axis of the staggered Grid
             */
            float2 cy = new float2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    /* Calculate staggered Node index */
                    int2 nodeIndexY = bottomLeftNodeY + new int2(x, y);
                    int index = math.mad(nodeIndexY.x, grid.Size.y + 1, nodeIndexY.y);

                    /*
                     * Calculate partial derivative of weight based on Parcel
                     * position inside the staggered Cell
                     */
                    float2 dw = new float2(
                        (y == 0) ? (1f - fractionalPositionY.y) : fractionalPositionY.y,
                        (x == 0) ? (1f - fractionalPositionY.x) : fractionalPositionY.x
                    );
                    if (x == 0) dw.x *= -1f;
                    if (y == 0) dw.y *= -1f;
                    dw /= grid.CellSize;
                    
                    cy += dw * grid.VelocityY[index];
                }
            }

            AffineState[i] = new float2x2(cx, cy);
        }
    }

    /// <summary>
    /// Update Parcels position based on their velocities
    /// </summary>
    /// <param name="grid">The staggered Grid to take the information from</param>
    /// <param name="dt">The time step of the simulation</param>
    public void Move(StaggeredGrid grid, float dt) {
        for (int i = 0; i < Count; ++i) {
            /* Calculate midpoint velocity */
            float2 v = Velocity[i];
            float2 vMid = v + math.mul(
                AffineState[i],
                v
            ) * (dt * 0.5f); // Half Step;
            float2 pos = Position[i] + vMid * dt;
            float2 size = (float2)grid.BoundedSize * grid.CellSize;
            
            // TODO: Check boundary conditions and correct Parcels velocity
            /* Correct the Parcel velocity if outside of the Grid bounds */
            if (pos.x < 0f || pos.x > size.x) {
                // float t = (pos.x < 0f) ? Position[i].x : (Position[i].x - size.x);
                // t /= math.distance(pos.x, Position[i].x);
                // v.x = math.lerp(0, v.x, t);
                // v.x = 0f;
                v.y *= (1f - Friction);
            }
            if (pos.y < 0 || pos.y > size.y) {
                // float t = (pos.y < 0f) ? Position[i].y : (Position[i].y - size.y);
                // t /= math.distance(pos.y, Position[i].y);
                v.x *= (1f - Friction);
                // v.y = 0f;
                // v.y = math.lerp(0, v.y, t);
            }

            /* Correct the Parcel position if outside of the Grid bounds */
            float epsilon = grid.CellSize * 0.01f;
            pos = math.clamp(pos, epsilon, size - epsilon);

            Position[i] = pos;
            Velocity[i] = v;
        }
    }
}
