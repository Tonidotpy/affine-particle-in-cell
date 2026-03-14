using UnityEngine;

namespace FluidSimulationGPU {
/// <summary>
/// Eulerian fluid simulation based on a staggered Grid
/// </summary>
public class FluidSimulation {
    FluidGridManager gridManager;
    float fluidDensity = 1.3f;       // kg/m^2
    float ambientTemperature = 300f; // K

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / (60f * timeStepMultiplier);
    float timeStepMultiplier = 1;
    int solverIterations = 15;

    /// <summary>
    /// Get the staggered Grid manager
    /// </summary>
    public FluidGridManager GridManager {
        get { return gridManager; }
    }

    /// <summary>
    /// Get or set the Successive Over-Relaxation multiplier used for pressure
    /// calculation.
    /// </summary>
    public float SOR {
        get { return gridManager.sorMultiplier; }
        set { gridManager.sorMultiplier = Mathf.Clamp(value, 1.1f, 1.9f); }
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

    /// <summary>
    /// Density of the fluid in [kg/m^2]
    /// </summary>
    public float FluidDensity {
        get { return fluidDensity; }
        set { fluidDensity = Mathf.Max(value, 1e-9f); }
    }

    /// <summary>
    /// Ambient temperature in °C
    /// </summary>
    public float AmbientTemperature {
        get { return ambientTemperature - 273.15f; }
        set { ambientTemperature = Mathf.Max(value + 273.15f, 0); }
    }

    public FluidSimulation(int gridWidth, int gridHeight, ComputeShader gridCompute) {
        gridManager = new FluidGridManager(gridWidth, gridHeight, gridCompute);
    }

    /// <summary>
    /// Run a single step of the fluid simulation
    /// </summary>
    public void RunStep() {
        UpdateGridSettings();
        gridManager.Setup();

        gridManager.AdvectVelocities(timeStep);
        gridManager.AdvectSmoke(timeStep);

        gridManager.SolvePressure(solverIterations, timeStep);
        gridManager.UpdateVelocities(timeStep);
    }

    public void Clean() {
        gridManager.ReleaseTextures();
    }

    void UpdateGridSettings() {
        gridManager.density = fluidDensity;
        gridManager.ambientTemperature = ambientTemperature;
    }
}
}
