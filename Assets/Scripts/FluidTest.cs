using UnityEngine;
using static UnityEngine.Mathf;

namespace FluidSimulation {
    [RequireComponent(typeof(FluidDrawer))]
    public class FluidTest : MonoBehaviour {
        [Header("Grid Settings")]
        public int width;
        public int height;
        public int cellSize = 1;

        [Header("Simulation Settings")]
        public int solverIterations = 1;

        FluidDrawer fluidDrawer;
        FluidGrid fluidGrid;
 
        void Start() {
            fluidDrawer = GetComponent<FluidDrawer>();
            fluidGrid = new FluidGrid(width, height, cellSize);
            fluidDrawer.SetFluidGridToVisualize(fluidGrid);

            Camera.main.orthographicSize = height * cellSize * 0.6f;
        }

        void Update() {
            Simulate();
            HandleInput();
        }

        void Simulate() {
            // Solve for pressure
            fluidGrid.SolvePressure(solverIterations);
            fluidGrid.UpdateVelocities();
            
            // Update visualization
            fluidDrawer.Visualize();
        }

        void HandleInput() {
            fluidDrawer.HandleInteraction();
        }
    }
}
