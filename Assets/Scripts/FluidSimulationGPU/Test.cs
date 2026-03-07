using UnityEngine;

namespace FluidSimulationGPU {
[RequireComponent(typeof(FluidRendererManager))]
public class Test : MonoBehaviour {
    FluidGridManager grid;
    FluidRendererManager fluidRenderer;

    public ComputeShader compute;
    public Vector2Int resolution = new(50, 50);
    public float timeStepMultiplier = 1;
    public int solverIterations = 15;

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    float timeStep => 1f / (60f * timeStepMultiplier);

    public void Start() {
        grid = new FluidGridManager(resolution.x, resolution.y, compute);
        fluidRenderer = GetComponent<FluidRendererManager>();
        fluidRenderer.SetGridToRender(grid);

        Camera.main.orthographicSize = resolution.y * fluidRenderer.cellSize * 0.6f;
    }

    public void Update() {
        grid.Setup();
        grid.SolvePressure(solverIterations, timeStep);
    }

    public void OnDestroy() {
        grid.ReleaseTextures();
    }
}
}
