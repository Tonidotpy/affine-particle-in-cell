using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Gauss-Seidel iterative method to solve the Poisson-Pressure equation
/// </summary>
public class GaussSeidelPressureSolver {
    public int MaxIterations { get; }

    /// <summary>
    /// Setup the Gauss-Seidel iterative method to solve the Poisson-Pressure
    /// equation
    /// </summary>
    /// <param name="maxIterations">The maximum number of iterations to solve the system</param>
    public GaussSeidelPressureSolver(int maxIterations) {
        MaxIterations = maxIterations;
    }

    /// <summary>
    /// Calculate the pressure given the grid information
    /// </summary>
    /// <param name="grid">The staggered Grid to solve the problem for</param>
    /// <param name="dt">The timestep of the simulation</param>
    public void Solve(StaggeredGrid grid, float dt) {
        for (int i = 0; i < MaxIterations; ++i) {
            /*
             * Iterate over each Cell taking into account the "ghost layer"
             * around the Grid
             */
            for (int x = 0; x < grid.Size.x; ++x) {
                for (int y = 0; y < grid.Size.y; ++y) {
                    int index = math.mad(x + 1, grid.Size.y + 1, y + 1);

                    if (grid.Type[index] == CellType.Air) {
                        grid.Pressure[index] = 0f;
                    }
                    else if (grid.Type[index] == CellType.Solid) {
                        float boundaryVelocity = 0f;
                        int fluidIndex = 0;
                        if (y == 0) {
                            int j = math.mad(x, grid.Size.y + 1, y + 1);
                            boundaryVelocity = grid.VelocityY[j];
                            fluidIndex = math.mad(x + 1, grid.Size.y + 1, y + 2);
                        }
                        else if (y == grid.Size.y - 1) {
                            int j = math.mad(x, grid.Size.y + 1, y - 1);
                            boundaryVelocity = grid.VelocityY[j];
                            fluidIndex = math.mad(x + 1, grid.Size.y + 1, y);
                        }
                        else if (x == 0) {
                            int j = math.mad(x + 1, grid.Size.y, y);
                            boundaryVelocity = grid.VelocityY[j];
                            fluidIndex = math.mad(x + 2, grid.Size.y + 1, y + 1);
                        }
                        else if (x == grid.Size.x - 1) {
                            int j = math.mad(x - 1, grid.Size.y, y);
                            boundaryVelocity = grid.VelocityY[j];
                            fluidIndex = math.mad(x, grid.Size.y + 1, y + 1);
                        }

                        float b = ((grid.FluidDensity * grid.CellSize) / dt) * boundaryVelocity;
                        grid.Pressure[index] = grid.Pressure[fluidIndex] + b;
                    }
                    else { 
                        // Calculate known value
                        int boundedIndex = math.mad(x - 1, grid.BoundedSize.y, y - 1);
                        float b = ((grid.CellArea * grid.FluidDensity) / dt) * grid.Divergence[boundedIndex];

                        // Calculate pressure value
                        int left = index - (grid.Size.y + 1);
                        int right = index + (grid.Size.y + 1);
                        int bottom = index - 1;
                        int top = index + 1;
                        grid.Pressure[index] = (grid.Pressure[left] + grid.Pressure[right] +
                            grid.Pressure[bottom] + grid.Pressure[top] - b) * 0.25f;
                    }
                }
            }
        }
    }
}
