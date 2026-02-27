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

    [Header("Simulation Settings")]
    public int solverIterations = 1;
    public float sor = 1.7f;
    public float timeStepMultiplier = 1f;
    public bool isSimulationPaused = false;

    [Header("Velocity Settings")]
    public float velocityStrenght = 10f;

    [Header("Smoke Settings")]
    public float smokeAmount = 0.2f;

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
        MouseRight: Add smoke");
    }

    void Start() {
        simulationRenderer = GetComponent<FluidRenderer>();
        simulation = new FluidSimulation(width, height);

        simulationRenderer.SetGridToRender(simulation.Grid);

        Camera.main.orthographicSize = height * simulationRenderer.CellSize * 0.6f;

        ShowUsage();
    }

    void Update() {
        simulation.SOR = sor;
        simulation.TimeStepMultiplier = timeStepMultiplier;
        simulation.SolverIterations = solverIterations;

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
            Vector2Int cellCenter = simulationRenderer.WorldToCellCenter(mousePosition);

            Vector2 mouseDelta = mousePosition - mousePositionOld;
            int offset = CeilToInt(simulationRenderer.inputRadius);
            for (int offy = -offset; offy <= offset; ++offy) {
                for (int offx = -offset; offx <= offset; ++offx) {
                    int i = cellCenter.x + offx;
                    int j = cellCenter.y + offy;
                    if (i < 0 || i >= width || j < 0 || j >= height)
                        continue;

                    Vector2 cellPosition = simulationRenderer.CellCenterToWorld(i, j);
                    float sqrRadius = simulationRenderer.inputRadius * simulationRenderer.inputRadius;
                    float weight = 1 - Mathf.Clamp01((cellPosition - mousePosition).sqrMagnitude / sqrRadius);
                    FluidGridMac grid = simulation.Grid;

                    float x = i;
                    float y = j;
                    Vector2 velocityDelta = mouseDelta * weight * velocityStrenght;
                    float newVelocityU = grid.GetVelocity(grid.velocityU, x - 0.5f, y, FluidGridMac.Axis.X) + velocityDelta.x;
                    float newVelocityV = grid.GetVelocity(grid.velocityV, x, y - 0.5f, FluidGridMac.Axis.Y) + velocityDelta.y;
                    grid.SetVelocity(grid.velocityU, x - 0.5f, y, FluidGridMac.Axis.X, newVelocityU);
                    grid.SetVelocity(grid.velocityV, x, y - 0.5f, FluidGridMac.Axis.Y, newVelocityV);
                }
            }
        }
        if (isMouseRightHeld) {
            Vector2Int cellCenter = simulationRenderer.WorldToCellCenter(mousePosition);
            simulation.Grid.AddSmokeAtPosition(cellCenter, smokeAmount, simulationRenderer.inputRadius);
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
                        grid.SetCellType(i, j, FluidGridMac.CellType.Solid);
                }
            }
        }
    }
}
}
