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
        // Position[0] = new float2(3f, 3f);
        // Position[1] = new float2(7, 3);
        // Velocity[0] = new float2(0, 1);
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
    /// <param name="grid">The staggered Grid to take the velocity information from</param>
    public void UpdateVelocity(StaggeredGrid grid) {
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing the Parcel staggered along the X axis
             * and its fractional position for the interpolation
             */
            float2 parcelX = Position[i] / grid.CellSize + new float2(1f, 0.5f);
            int2 indexX = (int2)math.floor(parcelX);
            float2 tX = parcelX - indexX;

            /*
             * Get X velocities of the box of Cells surrounding the Parcel
             */
            float2x2 ux = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexX.x + x, grid.Size.y, indexX.y + y);
                    ux[x][y] = grid.VelocityX[j];
                }
            }

            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing the Parcel staggered along the Y axis
             * and its fractional position for the interpolation
             */
            float2 parcelY = Position[i] / grid.CellSize + new float2(0.5f, 1f);
            int2 indexY = (int2)math.floor(parcelY);
            float2 tY = parcelY - indexY;

            /*
             * Get Y velocities of the box of Cells surrounding the Parcel
             */
            float2x2 uy = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexY.x + x, grid.Size.y + 1, indexY.y + y);
                    uy[x][y] = grid.VelocityY[j];
                }
            }

            /*
             * Update velocity using bilinear interpolation based on Parcel
             * fractional's position
             */
            Velocity[i] = new float2(
                math.lerp(
                    math.lerp(ux[0][0], ux[1][0], tX.x),
                    math.lerp(ux[0][1], ux[1][1], tX.x),
                    tX.y
                ),
                math.lerp(
                    math.lerp(uy[0][0], uy[1][0], tY.x),
                    math.lerp(uy[0][1], uy[1][1], tY.x),
                    tY.y
                )
            );
        }
    }

    /// <summary>
    /// Update Parcels affine state matrix
    /// </summary>
    /// <param name="grid">The staggered Grid to take the velocity information from</param>
    public void UpdateAffineState(StaggeredGrid grid) {
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing the Parcel staggered along both the X and Y axis
             * and its fractional position for the interpolation
             */
            float2 staggeredParcel = Position[i] / grid.CellSize + 0.5f;
            int2 staggeredIndex = (int2)math.floor(staggeredParcel);
            float2 staggeredFractionalPosition = staggeredParcel - staggeredIndex;

            /******************************************************************
             *              CHANGE IN X VELOCITY ALONG X DIRECTION
             *****************************************************************/
            /*
             * Calculate X velocity gradient for the four Cells surrounding
             * the Parcel
             */
            float2x2 dx = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int left = math.mad(staggeredIndex.x + x, grid.Size.y, staggeredIndex.y + y);
                    int right = math.mad(staggeredIndex.x + x + 1, grid.Size.y, staggeredIndex.y + y);
                    dx[x][y] = (grid.VelocityX[right] - grid.VelocityX[left]) / grid.CellSize;
                }
            }
            float C00 = math.lerp(
                math.lerp(dx[0][0], dx[1][0], staggeredFractionalPosition.x),
                math.lerp(dx[0][1], dx[1][1], staggeredFractionalPosition.x),
                staggeredFractionalPosition.y
            );

            /******************************************************************
             *              CHANGE IN Y VELOCITY ALONG Y DIRECTION
             *****************************************************************/

            /*
             * Calculate Y velocity gradient for the four Cells surrounding
             * the Parcel
             */
            float2x2 dy = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int bottom = math.mad(staggeredIndex.x + x, grid.Size.y + 1, staggeredIndex.y + y);
                    int top = math.mad(staggeredIndex.x + x, grid.Size.y + 1, staggeredIndex.y + y + 1);
                    dy[x][y] = (grid.VelocityY[top] - grid.VelocityY[bottom]) / grid.CellSize;
                }
            }
            float C11 = math.lerp(
                math.lerp(dy[0][0], dy[1][0], staggeredFractionalPosition.x),
                math.lerp(dy[0][1], dy[1][1], staggeredFractionalPosition.x),
                staggeredFractionalPosition.y
            );

            /******************************************************************
             *              CHANGE IN X VELOCITY ALONG Y DIRECTION
             *****************************************************************/

            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing a "virtual" Parcel placed slightly above the staggered
             * Parcel and its fractional position for the interpolation
             */
            float2 parcelTop = staggeredParcel + new float2(0, 0.5f);
            int2 indexTop = (int2)math.floor(parcelTop);
            float2 tTop = parcelTop - indexTop;

            /*
             * Calculate X velocity of the "virtual" Parcel
             */
            float2x2 uxTopStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexTop.x + x, grid.Size.y, indexTop.y + y);
                    uxTopStaggered[x][y] = grid.VelocityX[j];
                }
            }
            float uxTop = math.lerp( 
                math.lerp(uxTopStaggered[0][0], uxTopStaggered[1][0], tTop.x),
                math.lerp(uxTopStaggered[0][1], uxTopStaggered[1][1], tTop.x),
                tTop.y
            );

            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing a "virtual" Parcel placed slightly below the staggered
             * Parcel and its fractional position for the interpolation
             */
            float2 parcelBottom = staggeredParcel - new float2(0, 0.5f);
            int2 indexBottom = (int2)math.floor(parcelBottom);
            float2 tBottom = parcelBottom - indexBottom;

            /*
             * Calculate X velocity of the "virtual" Parcel
             */
            float2x2 uxBottomStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexBottom.x + x, grid.Size.y, indexTop.y + y);
                    uxBottomStaggered[x][y] = grid.VelocityX[j];
                }
            }
            float uxBottom = math.lerp( 
                math.lerp(uxBottomStaggered[0][0], uxBottomStaggered[1][0], tBottom.x),
                math.lerp(uxBottomStaggered[0][1], uxBottomStaggered[1][1], tBottom.x),
                tBottom.y
            );
            float C10 = (uxTop - uxBottom) / grid.CellSize;

            /******************************************************************
             *              CHANGE IN Y VELOCITY ALONG X DIRECTION
             *****************************************************************/

            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing a "virtual" Parcel placed slightly to the right of the
             * staggered Parcel and its fractional position for the interpolation
             */
            float2 parcelRight = staggeredParcel + new float2(0.5f, 0);
            int2 indexRight = (int2)math.floor(parcelRight);
            float2 tRight = parcelRight - indexRight;

            /*
             * Calculate Y velocity of the "virtual" Parcel
             */
            float2x2 uyRightStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexRight.x + x, grid.Size.y + 1, indexRight.y + y);
                    uyRightStaggered[x][y] = grid.VelocityY[j];
                }
            }
            float uyRight = math.lerp( 
                math.lerp(uyRightStaggered[0][0], uyRightStaggered[1][0], tRight.x),
                math.lerp(uyRightStaggered[0][1], uyRightStaggered[1][1], tRight.x),
                tRight.y
            );

            /*
             * Calculate indices of the Bottom-Left Cell of the 2x2 box
             * enclosing a "virtual" Parcel placed slightly to the left of the
             * staggered Parcel and its fractional position for the interpolation
             */
            float2 parcelLeft = staggeredParcel - new float2(0.5f, 0);
            int2 indexLeft = (int2)math.floor(parcelLeft);
            float2 tLeft = parcelLeft - indexLeft;

            /*
             * Calculate Y velocity of the "virtual" Parcel
             */
            float2x2 uyLeftStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexLeft.x + x, grid.Size.y + 1, indexLeft.y + y);
                    uyLeftStaggered[x][y] = grid.VelocityY[j];
                }
            }
            float uyLeft = math.lerp( 
                math.lerp(uyLeftStaggered[0][0], uyLeftStaggered[1][0], tLeft.x),
                math.lerp(uyLeftStaggered[0][1], uyLeftStaggered[1][1], tLeft.x),
                tLeft.y
            );
            float C01 = (uyRight - uyLeft) / grid.CellSize;

            /* Update affine matrix with calculated values */
            AffineState[i] = new float2x2(C00, C01, C10, C11);
        }
    }

    /// <summary>
    /// Update Parcels position based on their velocities
    /// </summary>
    /// <param name="grid">The staggered Grid to take the information from</param>
    /// <param name="dt">The time step of the simulation</param>
    public void Move(StaggeredGrid grid, float dt) {
        for (int i = 0; i < Count; ++i) {
            float2 v = Velocity[i];
            /* Calculate midpoint velocity */
            float2 vMid = v + math.mul(
                AffineState[i],
                Velocity[i] * dt * 0.5f // Half-Step
            );
            float2 pos = Position[i] + vMid * dt;

            /* Correct the Parcel position if outside of the Grid bounds */
            float2 size = (float2)grid.BoundedSize * grid.CellSize;
            float epsilon = grid.CellSize * 0.01f;
            pos = math.clamp(pos, 0, size - epsilon);
            
            /* Correct the Parcel velocity if outside of the Grid bounds */
            if (pos.x < 0 || pos.x > size.x) {
                v.x = 0;
                v.y *= (1f - Friction);
            }
            if (pos.y < 0 || pos.y > size.y) {
                v.x *= (1f - Friction);
                v.y = 0;
            }

            Position[i] = pos;
            Velocity[i] = v;
        }
    }
}
