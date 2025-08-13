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
            for (int x = 1; x < grid.Size.x + 1; ++x) {
                for (int y = 1; y < grid.Size.y + 1; ++y) {
                    int index = math.mad(x, (grid.Size.y + 2), y);
                    int boundedIndex = math.mad(x - 1, grid.Size.y, y - 1);
                    
                    // Calculate known value
                    float b = grid.Mass[index] / dt * grid.Divergence[boundedIndex];

                    // Calculate pressure value
                    int left = index - (grid.Size.y + 2);
                    int right = index + (grid.Size.y + 2);
                    int bottom = index - 1;
                    int top = index + 1;
                    grid.Pressure[index] = ((grid.Pressure[left] + grid.Pressure[right] +
                        grid.Pressure[bottom] + grid.Pressure[top]) - b) * 0.25f;
                }
            }
        }
    }
}
