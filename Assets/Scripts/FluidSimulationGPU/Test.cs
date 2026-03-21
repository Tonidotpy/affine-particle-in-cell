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
    public Vector2 gravity = new Vector2(0, -9.81f); // m/s^2
    public float ambientTemperature = 25f;           // °C
    public bool isSimulationPaused = false;

    [Header("Velocity Settings")]
    public float velocityMultiplier = 5f;

    [Header("Smoke Settings")]
    public float smokeAmount = 0.2f;
    public float smokeTemperature = 26f; // °C
    public float smokeDiffusionMultiplier = 0.3f;
    public float smokeDecayMultiplier = 1f;
    public float temperatureDiffusionMultiplier = 1f;
    public float temperatureDecayMultiplier = 1f;

    FluidSimulation simulation;
    FluidRendererManager simulationRenderer;

    bool shouldRunSimulationStepOnce = false;
    bool isMouseLeftHeld = false;
    Vector2 mousePositionOld = Vector2.zero;

    float CelsiusToKelvin(float temperatureCelsius) {
        return Mathf.Max(0, temperatureCelsius + 273.15f);
    }

    void Start() {
        simulation = new FluidSimulation(resolution.x, resolution.y, compute);
        simulationRenderer = GetComponent<FluidRendererManager>();
        simulationRenderer.SetGridToRender(simulation.GridManager);

        Camera.main.orthographicSize = resolution.y * simulationRenderer.cellSize * 0.6f;
    }

    void Update() {
        UpdateSimulationSettings();

        if (!isSimulationPaused || shouldRunSimulationStepOnce) {
            shouldRunSimulationStepOnce = false;
            simulation.RunStep();
        }
        HandleInput();
    }

    void OnDestroy() {
        simulation.Clean();
    }

    void UpdateSimulationSettings() {
        simulation.SOR = sor;
        simulation.TimeStepMultiplier = timeStepMultiplier;
        simulation.SolverIterations = solverIterations;
        simulation.Gravity = gravity;
        simulation.FluidDensity = fluidDensity;
        simulation.AmbientTemperature = ambientTemperature;
        simulation.SmokeDiffusionMultiplier = smokeDiffusionMultiplier;
        simulation.SmokeDecayMultiplier = smokeDecayMultiplier;
        simulation.TemperatureDiffusionMultiplier = temperatureDiffusionMultiplier;
        simulation.TemperatureDecayMultiplier = temperatureDecayMultiplier;
    }

    void HandleInput() {
        Vector2 mousePosition = InputHelper.MousePosWorld;
        isMouseLeftHeld = InputHelper.IsMouseHeld(MouseButton.Left);
        bool isMouseRightHeld = InputHelper.IsMouseHeld(MouseButton.Right);
        float mouseScrollDelta = InputHelper.MouseScrollDelta.y;
        float mouseInputRadius = simulationRenderer.inputRadius / simulationRenderer.cellSize;

        if (InputHelper.IsKeyDownThisFrame(KeyCode.Tab)) {
            simulationRenderer.CycleVisualizationMode(InputHelper.ShiftIsHeld);
        }
        if (Input.GetKeyDown(KeyCode.Space)) {
            isSimulationPaused = !isSimulationPaused;
        }
        if (isSimulationPaused && Input.GetKeyDown(KeyCode.N)) {
            shouldRunSimulationStepOnce = true;
        }

        if (isMouseLeftHeld) {
            Vector2Int cellCenter = simulationRenderer.WorldToCellCenter(mousePosition);
            Vector2 mouseDelta = mousePosition - mousePositionOld;
            simulation.GridManager.SetVelocityAtPosition(cellCenter, mouseInputRadius, velocityMultiplier * mouseDelta);
        }

        if (isMouseRightHeld) {
            Vector2Int cellCenter = simulationRenderer.WorldToCellCenter(mousePosition);
            simulation.GridManager.AddSmokeAtPosition(cellCenter, mouseInputRadius, smokeAmount,
                                                      simulationRenderer.smokeColor, CelsiusToKelvin(smokeTemperature));
        }

        simulationRenderer.inputRadius = Mathf.Max(0, simulationRenderer.inputRadius + mouseScrollDelta * 0.1f);

        mousePositionOld = mousePosition;

        simulation.HandleInput();
        simulationRenderer.RenderInput(mousePositionOld, isMouseLeftHeld);
    }
}
}
