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
    /// <param name="grid">The staggered Grid to take the velocity information from</param>
    public void UpdateVelocity(StaggeredGrid grid) {
        for (int i = 0; i < Count; ++i) {
            /*
             * Calculate Parcel fractional position relative to the grid of Cell
             * centers, these values ranges from 0 to 1
             */
            float2 baseParcel = Position[i] / grid.CellSize + 0.5f;
            int2 index = (int2)math.floor(baseParcel) - 1;
            float2 t = baseParcel - index;

            /*
             * Get X and Y velocities of the box of Cells surrounding the Parcel
             */
            float2x2 ux = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(index.x + x, grid.Size.y, index.y + y);
                    ux[x][y] = grid.VelocityX[j];
                }
            }
            float2x2 uy = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(index.x + x, grid.BoundedSize.y + 1, index.y + y);
                    uy[x][y] = grid.VelocityY[j];
                }
            }

            /*
             * Update velocity using bilinear interpolation base on Parcel
             * fractional's position
             */
            Velocity[i] = new float2(
                math.lerp(
                    math.lerp(ux[0][0], ux[1][0], t.x),
                    math.lerp(ux[0][1], ux[1][1], t.x),
                    t.y
                ),
                math.lerp(
                    math.lerp(uy[0][0], uy[1][0], t.x),
                    math.lerp(uy[0][1], uy[1][1], t.x),
                    t.y
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
            /******************************************************************
             *              CHANGE IN X VELOCITY ALONG X DIRECTION
             *****************************************************************/


            /*
             * Calculate indices of the Bottom-Left Cell enclosing the Parcel
             * and its fractional position for the interpolation
             */
            float2 baseParcel = Position[i] / grid.CellSize + 0.5f;
            int2 index = (int2)math.floor(baseParcel) - 1;
            float2 t = baseParcel - index;

            /*
             * Calculate X velocity gradient for the four Cells surrounding
             * the Parcel
             */
            float2x2 dx = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int left = math.mad(index.x + x, grid.Size.y, index.y + y);
                    int right = math.mad(index.x + x + 1, grid.Size.y, index.y + y);
                    dx[x][y] = (grid.VelocityX[right] - grid.VelocityX[left]) / grid.CellSize;
                }
            }
            float C00 = math.lerp(
                math.lerp(dx[0][0], dx[1][0], t.x),
                math.lerp(dx[0][1], dx[1][1], t.x),
                t.y
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
                    int bottom = math.mad(index.x + x, grid.BoundedSize.y + 1, index.y + y);
                    int top = math.mad(index.x + x, grid.BoundedSize.y + 1, index.y + y + 1);
                    dy[x][y] = (grid.VelocityY[top] - grid.VelocityY[bottom]) / grid.CellSize;
                }
            }
            float C11 = math.lerp(
                math.lerp(dy[0][0], dy[1][0], t.x),
                math.lerp(dy[0][1], dy[1][1], t.x),
                t.y
            );

            /******************************************************************
             *              CHANGE IN X VELOCITY ALONG Y DIRECTION
             *****************************************************************/

            /*
             * Calculate indices of the Bottom-Left Cell enclosing a "virtual"
             * Parcel placed slightly above the current Parcel and its
             * fractional position for the interpolation
             */
            float2 parcelTop = baseParcel + new float2(0, 0.5f);
            int2 indexTop = (int2)math.floor(parcelTop) - 1;
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
             * Calculate indices of the Bottom-Left Cell enclosing a "virtual"
             * Parcel placed slightly below the current Parcel and its
             * fractional position for the interpolation
             */
            float2 parcelBottom = baseParcel - new float2(0, 0.5f);
            int2 indexBottom = (int2)math.floor(parcelBottom) - 1;
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
             * Calculate indices of the Bottom-Left Cell enclosing a "virtual"
             * Parcel placed slightly to the right the current Parcel and its
             * fractional position for the interpolation
             */
            float2 parcelRight = baseParcel + new float2(0.5f, 0);
            int2 indexRight = (int2)math.floor(parcelRight) - 1;
            float2 tRight = parcelRight - indexRight;

            /*
             * Calculate Y velocity of the "virtual" Parcel
             */
            float2x2 uyRightStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexRight.x + x, grid.BoundedSize.y + 1, indexRight.y + y);
                    uyRightStaggered[x][y] = grid.VelocityY[j];
                }
            }
            float uyRight = math.lerp( 
                math.lerp(uyRightStaggered[0][0], uyRightStaggered[1][0], tRight.x),
                math.lerp(uyRightStaggered[0][1], uyRightStaggered[1][1], tRight.x),
                tRight.y
            );

            /*
             * Calculate indices of the Bottom-Left Cell enclosing a "virtual"
             * Parcel placed slightly to the left of the current Parcel and its
             * fractional position for the interpolation
             */
            float2 parcelLeft = baseParcel - new float2(0.5f, 0);
            int2 indexLeft = (int2)math.floor(parcelLeft) - 1;
            float2 tLeft = parcelLeft - indexLeft;

            /*
             * Calculate Y velocity of the "virtual" Parcel
             */
            float2x2 uyLeftStaggered = new float2x2();
            for (int x = 0; x < 2; ++x) {
                for (int y = 0; y < 2; ++y) {
                    int j = math.mad(indexLeft.x + x, grid.BoundedSize.y + 1, indexLeft.y + y);
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
}
