using UnityEngine;

namespace FluidSimulationRefactor {
public class FluidSimulation {
    FluidGridMac grid;

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / (60f * timeStepMultiplier);
    float timeStepMultiplier = 1;
    int solverIterations = 1;

    public FluidGridMac Grid {
        get { return grid; }
    }

    public float SOR {
        get { return grid.SorMultiplier; }
        set { grid.SorMultiplier = Mathf.Clamp(value, 1.1f, 1.9f); }
    }

    public float TimeStepMultiplier {
        get { return timeStepMultiplier; }
        set { timeStepMultiplier = Mathf.Max(value, 1e-9f); }
    }

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

    public void Reset() {
        grid.ClearVelocities();
        grid.ClearPressure();
        grid.ClearSmoke();
    }
}
}
