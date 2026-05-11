using System;
using UnityEngine;

namespace FluidSimulationGPU {
/// <summary>
/// Eulerian fluid simulation based on a staggered Grid
/// </summary>
public class FluidSimulation {
    FluidGridManager gridManager;
    Vector2 gravity = new Vector2(0, -9.81f); // m/s^2
    float fluidDensity = 1.3f;                // kg/m^2

    float ambientTemperature = 300f;          // K
    float smokeDiffusionMultiplier = 0.3f;
    float smokeDecayMultiplier = 1f;
    float smokeBuoyancyMultiplier = 1f;
    float temperatureDiffusionMultiplier = 1f;
    float temperatureDecayMultiplier = 1f;
    float temperatureBuoyancyMultiplier = 1f;

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / (60f * timeStepMultiplier);
    float timeStepMultiplier = 1;
    int solverIterations = 15;

    public bool CloseLeftEdge { get; set; }
    public bool CloseBottomEdge { get; set; }
    public bool CloseRightEdge { get; set; }
    public bool CloseTopEdge { get; set; }

    public FluidObstacle[] Obstacles { get; set; }

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
    /// Acceleration to apply to the whole fluid [m/s^2]
    /// </summary>
    public Vector2 Gravity {
        get { return gravity; }
        set { gravity = value; }
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

    /// <summary>
    /// Smoke diffusion multiplier
    /// </summary>
    public float SmokeDiffusionMultiplier {
        get { return smokeDiffusionMultiplier; }
        set { smokeDiffusionMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Smoke decay multiplier
    /// </summary>
    public float SmokeDecayMultiplier {
        get { return smokeDecayMultiplier; }
        set { smokeDecayMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Smoke buoyancy force multiplier
    /// </summary>
    public float SmokeBuoyancyMultiplier {
        get { return smokeBuoyancyMultiplier; }
        set { smokeBuoyancyMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Temperature diffusion multiplier
    /// </summary>
    public float TemperatureDiffusionMultiplier {
        get { return temperatureDiffusionMultiplier; }
        set { temperatureDiffusionMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Temperature decay multiplier
    /// </summary>
    public float TemperatureDecayMultiplier {
        get { return temperatureDecayMultiplier; }
        set { temperatureDecayMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Temperature buoyancy force multiplier
    /// </summary>
    public float TemperatureBuoyancyMultiplier {
        get { return temperatureBuoyancyMultiplier; }
        set { temperatureBuoyancyMultiplier = Mathf.Max(value, 0); }
    }

    public FluidSimulation(int gridWidth, int gridHeight, ComputeShader gridCompute) {
        gridManager = new FluidGridManager(gridWidth, gridHeight, gridCompute);
    }

    /// <summary>
    /// Setup the simulation before actually running the step
    /// </summary>
    public void SetupStep() {
        UpdateGridSettings();
        gridManager.Setup();
    }

    /// <summary>
    /// Run a single step of the fluid simulation
    /// </summary>
    public void RunStep() {
        // Since the fluid may be divergent it is needed to remove the divergence
        // based on pressure differences
        gridManager.SolvePressure(solverIterations, timeStep);
        gridManager.UpdateVelocities(timeStep);

        // For advection the fluid is required to be divergence free
        // so this step is done immediately after the pressure correction
        gridManager.AdvectSmoke(timeStep);
        gridManager.AdvectVelocities(timeStep);

        // Any other step may increase the divergence of the fluid
        gridManager.AddSmokeFromSources(timeStep);
        gridManager.AddBuoyancyForce(timeStep);
    }

    public void HandleInput() {
        gridManager.HandleInput();
    }

    public void HandleObstacles() {
        gridManager.UpdateObstacles();
    }

    public void Clean() {
        gridManager.ReleaseTextures();
    }

    void UpdateGridSettings() {
        gridManager.closeLeftEdge = CloseLeftEdge;
        gridManager.closeBottomEdge = CloseBottomEdge;
        gridManager.closeRightEdge = CloseRightEdge;
        gridManager.closeTopEdge = CloseTopEdge;

        gridManager.gravity = gravity;
        gridManager.density = fluidDensity;
        gridManager.ambientTemperature = ambientTemperature;
        gridManager.smokeDiffusionMultiplier = smokeDiffusionMultiplier;
        gridManager.smokeDecayMultiplier = smokeDecayMultiplier;
        gridManager.smokeBuoyancyMultiplier = smokeBuoyancyMultiplier;
        gridManager.temperatureDiffusionMultiplier = temperatureDiffusionMultiplier;
        gridManager.temperatureDecayMultiplier = temperatureDecayMultiplier;
        gridManager.temperatureBuoyancyMultiplier = temperatureBuoyancyMultiplier;
        gridManager.obstacles = Obstacles;
    }
}
}
