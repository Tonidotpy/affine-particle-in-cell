using UnityEngine;
using Seb.Helpers;
using static UnityEngine.Mathf;

namespace FluidSimulationCPU {
/// <summary>
/// Fluid simulation test
/// </summary>
[RequireComponent(typeof(FluidRenderer))]
public class FluidTest : MonoBehaviour {
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 5;
    public int parcelsCount = 50;
    public float fluidDensity = 1.3f; // kg/m^2

    [Header("Simulation Settings")]
    public int solverIterations = 1;
    public float sor = 1.7f;
    public float timeStepMultiplier = 1f;
    public bool isSimulationPaused = false;
    public Vector2 gravity = Vector2.down * 9.81f; // m/s^2
    public float ambientTemperature = 25f;         // °C

    [Header("Velocity Settings")]
    public float velocityStrenght = 10f;

    [Header("Smoke Settings")]
    public float smokeAmount = 0.2f;
    public float smokeBuoyancyMultiplier = 1f;
    public float temperatureBuoyancyMultiplier = 1f;

    FluidSimulation simulation;
    FluidRenderer simulationRenderer;

    bool isMouseLeftHeld = false;
    Vector2 mousePositionOld = Vector2.zero;
    bool shouldRunSimulationStepOnce = false;

    void ShowUsage() {
        Debug.Log(@"Controls:
        Tab: Cycle visualization mode forward
        Shift+Tab: Cycle visualization mode backward
        Space: Pause/Unpause simulation
        N: When paused run a single simulation step
        C: Reset the simulation
        S: Update fluid obstacles in the Grid
        Z: Clear all the obstacles
        MouseLeft: Add velocity
        MouseRight: Add smoke
        Shift+MouseRight: Remove smoke");
    }

    void Start() {
        simulationRenderer = GetComponent<FluidRenderer>();
        simulation = new FluidSimulation(width, height, parcelsCount);

        simulationRenderer.SetGridToRender(simulation.Grid);
        simulationRenderer.SetParcelsToRender(simulation.Parcels);

        Camera.main.orthographicSize = height * simulationRenderer.CellSize * 0.6f;

        ShowUsage();
    }

    void Update() {
        simulation.SOR = sor;
        simulation.TimeStepMultiplier = timeStepMultiplier;
        simulation.SolverIterations = solverIterations;
        simulation.FluidDensity = fluidDensity;
        simulation.Gravity = gravity / simulationRenderer.CellSize;
        simulation.AmbientTemperature = ambientTemperature;
        simulation.SmokeBuoyancyMultiplier = smokeBuoyancyMultiplier;
        simulation.TemperatureBuoyancyMultiplier = temperatureBuoyancyMultiplier;

        if (!isSimulationPaused || shouldRunSimulationStepOnce) {
            shouldRunSimulationStepOnce = false;
            simulation.RunStep();
        }

        HandleInput();
        simulationRenderer.Render(mousePositionOld, isMouseLeftHeld);
    }

    /// <summary>
    /// Handle user input such as mouse and keyboard inputs to interact with the simulation
    /// </summary>
    void HandleInput() {
        Vector2 mousePosition = InputHelper.MousePosWorld;
        isMouseLeftHeld = InputHelper.IsMouseHeld(MouseButton.Left);
        bool isMouseRightHeld = InputHelper.IsMouseHeld(MouseButton.Right);
        float mouseScrollDelta = InputHelper.MouseScrollDelta.y;
        float mouseInputRadius = simulationRenderer.inputRadius / simulationRenderer.CellSize;

        if (InputHelper.IsKeyDownThisFrame(KeyCode.Tab)) {
            simulationRenderer.CycleVisualizationMode(InputHelper.ShiftIsHeld);
        }
        if (Input.GetKeyDown(KeyCode.Space)) {
            isSimulationPaused = !isSimulationPaused;
        }
        if (isSimulationPaused && Input.GetKeyDown(KeyCode.N)) {
            shouldRunSimulationStepOnce = true;
        }
        if (Input.GetKeyDown(KeyCode.C)) {
            simulation.Reset();
        }

        if (Input.GetKeyDown(KeyCode.S)) {
            var obstacles = GameObject.FindObjectsByType<FluidObstacle>(FindObjectsSortMode.None);
            UpdateObstacles(obstacles);
        }
        if (Input.GetKeyDown(KeyCode.Z)) {
            simulation.Grid.ClearObstacles();
        }

        if (isMouseLeftHeld) {
            Vector2 mouseDelta = mousePosition - mousePositionOld;
            FluidParcels parcels = simulation.Parcels;
            for (int i = 0; i < parcels.count; ++i) {
                Vector2 p = parcels.position[i];
                Vector2 position = simulationRenderer.CellCenterToWorld(p.x, p.y);
                float sqrDistance = (position - mousePosition).sqrMagnitude;
                float sqrRadius = mouseInputRadius * mouseInputRadius;

                if (sqrDistance <= sqrRadius) {
                    float weight = 1f - Mathf.Clamp01(sqrDistance / sqrRadius);
                    Vector2 velocityDelta = mouseDelta * weight * velocityStrenght;
                    parcels.velocity[i] += velocityDelta;
                }
            }
        }
        if (isMouseRightHeld) {
            FluidParcels parcels = simulation.Parcels;
            for (int i = 0; i < parcels.count; ++i) {
                Vector2 p = parcels.position[i];
                Vector2 position = simulationRenderer.CellCenterToWorld(p.x, p.y);
                float sqrDistance = (position - mousePosition).sqrMagnitude;
                float sqrRadius = mouseInputRadius * mouseInputRadius;

                if (sqrDistance <= sqrRadius) {
                    float weight = 1f - Mathf.Clamp01(sqrDistance / sqrRadius);
                    float massDelta = weight * smokeAmount;
                    float mass = parcels.mass[i];
                    mass += InputHelper.ShiftIsHeld ? -massDelta : massDelta;
                    parcels.mass[i] = Mathf.Max(1f, mass);
                }
            }
        }

        simulationRenderer.inputRadius = Mathf.Max(0, simulationRenderer.inputRadius + mouseScrollDelta * 0.1f);

        mousePositionOld = mousePosition;
    }

    /// <summary>
    /// Update the simulation Grid to match the given list of obstacles objects
    /// </summary>
    /// <param name="obstacles">List of fluid obstacles to update</param>
    void UpdateObstacles(FluidObstacle[] obstacles) {
        FluidGridMac grid = simulation.Grid;
        grid.ClearObstacles();

        foreach (FluidObstacle obstacle in obstacles) {
            Bounds bounds = obstacle.bounds;
            Vector2Int lbCellCenter = simulationRenderer.WorldToCellCenter(bounds.min);
            Vector2Int rtCellCenter = simulationRenderer.WorldToCellCenter(bounds.max);

            for (int i = lbCellCenter.x; i <= rtCellCenter.x; ++i) {
                for (int j = lbCellCenter.y; j <= rtCellCenter.y; ++j) {
                    Vector2 pos = simulationRenderer.CellCenterToWorld(i, j);
                    if (obstacle.Contains(pos))
                        grid.SetCellCenterValue(grid.cellType, i, j, FluidGridMac.CellType.Solid);
                }
            }
        }
    }
}
}
