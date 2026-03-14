using UnityEngine;
using Seb.Helpers;

namespace FluidSimulationGPU {
[RequireComponent(typeof(FluidRendererManager))]
public class Test : MonoBehaviour {
    [Header("Grid Settings")]
    public ComputeShader compute;
    public Vector2Int resolution = new(50, 50);
    public float fluidDensity = 1.3f; // kg/m^2

    [Header("Simulation Settings")]
    public int solverIterations = 15;
    public float sor = 1.7f;
    public float timeStepMultiplier = 1f;
    public float ambientTemperature = 25f; // °C

    FluidSimulation simulation;
    FluidRendererManager simulationRenderer;

    void Start() {
        simulation = new FluidSimulation(resolution.x, resolution.y, compute);
        simulationRenderer = GetComponent<FluidRendererManager>();
        simulationRenderer.SetGridToRender(simulation.GridManager);

        Camera.main.orthographicSize = resolution.y * simulationRenderer.cellSize * 0.6f;
    }

    void Update() {
        UpdateSimulationSettings();

        simulation.RunStep();
        HandleInput();
    }

    void OnDestroy() {
        simulation.Clean();
    }

    void UpdateSimulationSettings() {
        simulation.SOR = sor;
        simulation.TimeStepMultiplier = timeStepMultiplier;
        simulation.SolverIterations = solverIterations;
        simulation.FluidDensity = fluidDensity;
        simulation.AmbientTemperature = ambientTemperature;
    }

    void HandleInput() {
        if (InputHelper.IsKeyDownThisFrame(KeyCode.Tab)) {
            simulationRenderer.CycleVisualizationMode(InputHelper.ShiftIsHeld);
        }
    }
}
}
