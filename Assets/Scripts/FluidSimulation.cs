using UnityEngine;

namespace FluidSimulation {
/// <summary>
/// Eulerian fluid simulation based on a staggered Grid
/// </summary>
public class FluidSimulation {
    FluidGridMac grid;

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / (60f * timeStepMultiplier);
    float timeStepMultiplier = 1;
    int solverIterations = 1;

    /// <summary>
    /// Get the staggered Grid object
    /// </summary>
    public FluidGridMac Grid {
        get { return grid; }
    }

    /// <summary>
    /// Get or set the Successive Over-Relaxation multiplier used for pressure
    /// calculation.
    /// </summary>
    public float SOR {
        get { return grid.SorMultiplier; }
        set { grid.SorMultiplier = Mathf.Clamp(value, 1.1f, 1.9f); }
    }

    /// <summary>
    /// Get or set the time step multiplier.
    /// The higher the multiplier the slower the simulation.
    /// </summary>
    public float TimeStepMultiplier {
        get { return timeStepMultiplier; }
        set { timeStepMultiplier = Mathf.Max(value, 1e-9f); }
    }

    /// <summary>
    /// Get or set the total number of iterations of the pressure solver.
    /// </summary>
    public int SolverIterations {
        get { return solverIterations; }
        set { solverIterations = Mathf.Max(value, 1); }
    }

    public FluidSimulation(int gridWidth, int gridHeight) {
        grid = new FluidGridMac(gridWidth, gridHeight);
    }

    /// <summary>
    /// Run a single step of the fluid simulation
    /// </summary>
    public void RunStep() {
        grid.SolvePressure(solverIterations, timeStep);
        grid.UpdateVelocities(timeStep);
        grid.AdvectVelocities(timeStep);
        grid.AdvectSmoke(timeStep);
    }

    /// <summary>
    /// Reset the simulation to its original state.
    /// </summary>
    public void Reset() {
        grid.ClearVelocities();
        grid.ClearPressure();
        grid.ClearSmoke();
    }
}
}
