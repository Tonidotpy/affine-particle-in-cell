using UnityEngine;

namespace FluidSimulationCPU {
/// <summary>
/// Eulerian fluid simulation based on a staggered Grid
/// </summary>
public class FluidSimulation {
    FluidParcels parcels;
    FluidGridMac grid;

    float fluidDensity = 1.3f;       // kg/m^2
    float ambientTemperature = 300f; // K
    Vector2[] externalAccelerations; // m/s^2
    float smokeBuoyancyMultiplier = 1f;
    float temperatureBuoyancyMultiplier = 1f;

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / Mathf.Max(60f * timeStepMultiplier, grid.MaxVelocity * 1.1f);
    float timeStepMultiplier = 1;
    int solverIterations = 1;

    /// <summary>
    /// Get the staggered Grid object
    /// </summary>
    public FluidGridMac Grid {
        get { return grid; }
    }

    /// <summary>
    /// Get the fluid Parcels object
    /// </summary>
    public FluidParcels Parcels {
        get { return parcels; }
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
    /// Buoyancy formula smoke concentration multiplier
    /// </summary>
    public float SmokeBuoyancyMultiplier {
        get { return smokeBuoyancyMultiplier; }
        set { smokeBuoyancyMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Buoyancy formula temperature multiplier
    /// </summary>
    public float TemperatureBuoyancyMultiplier {
        get { return temperatureBuoyancyMultiplier; }
        set { temperatureBuoyancyMultiplier = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Gravity acceleration applied to all the fluid
    /// </summary>
    public Vector2 Gravity { get; set; }

    public FluidSimulation(int gridWidth, int gridHeight, int parcelsCount) {
        parcels = new FluidParcels(parcelsCount);
        grid = new FluidGridMac(gridWidth, gridHeight);

        for (int i = 0; i < gridWidth; ++i) {
            for (int j = 0; j < gridHeight; ++j) {
                grid.temperature[i, j] = ambientTemperature;
            }
        }

        externalAccelerations = new Vector2[1];
    }

    /// <summary>
    /// Run a single step of the fluid simulation
    /// </summary>
    public void RunStep() {
        UpdateGridParameters();

        grid.TransferParcelsData(parcels);

        // Add buoyancy forces -> fluid has non-zero divergence
        // grid.AddBuoyancyForce(timeStep);

        // Add forces -> fluid has non-zero divergence (replaced by buoyancy)
        // externalAccelerations[0] = Gravity;
        // grid.AddExternalBodyForce(externalAccelerations, timeStep);

        // Remove divergence based on pressure difference -> fluid becomes divergence free again
        grid.SolvePressure(solverIterations, timeStep);
        grid.UpdateVelocities(timeStep);

        parcels.TransferGridData(grid);
        parcels.Advect(grid, timeStep);

        // Advect quantities -> fluid MUST BE divergence free
        // grid.AdvectVelocities(timeStep);
        // grid.AdvectTemperature(timeStep);
        // grid.AdvectSmoke(timeStep);
    }

    void UpdateGridParameters() {
        grid.Density = fluidDensity;
        grid.AmbientTemperature = ambientTemperature;
        grid.SmokeBuoyancyMultiplier = smokeBuoyancyMultiplier;
        grid.TemperatureBuoyancyMultiplier = temperatureBuoyancyMultiplier;
    }

    /// <summary>
    /// Reset the simulation to its original state.
    /// </summary>
    public void Reset() {
        grid.ClearVelocities();
        grid.ClearPressure();
        grid.ClearSmoke();
        grid.ClearTemperature();
    }
}
}
