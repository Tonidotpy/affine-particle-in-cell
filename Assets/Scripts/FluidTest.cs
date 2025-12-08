using UnityEngine;
using static UnityEngine.Mathf;

namespace FluidSimulation {
    [RequireComponent(typeof(FluidDrawer))]
    public class FluidTest : MonoBehaviour {
        [Header("Parcels Settings")]
        public int parcelsCount = 10;

        [Header("Grid Settings")]
        public int width;
        public int height;
        public float cellSize = 1;

        [Header("Simulation Settings")]
        public int solverIterations = 1;
        public float sor = 1.7f;
        public float timeStepMultiplier = 1f;

        FluidDrawer fluidDrawer;
        FluidParcels fluidParcels;
        FluidGrid fluidGrid;
 
        void Start() {
            fluidDrawer = GetComponent<FluidDrawer>();
            fluidParcels = new FluidParcels(parcelsCount);
            fluidGrid = new FluidGrid(width, height, cellSize);

            fluidParcels.PairGrid(fluidGrid);
            fluidGrid.PairParcels(fluidParcels);

            fluidDrawer.SetParcelsToVisualize(fluidParcels);
            fluidDrawer.SetFluidGridToVisualize(fluidGrid);

            Camera.main.orthographicSize = height * cellSize * 0.6f;
        }

        void Update() {
            Simulate();
            HandleInput();
        }

        void Simulate() {
            fluidGrid.timeStepMultiplier = timeStepMultiplier;
            fluidGrid.SOR = sor;

            // Reset grid values
            fluidGrid.Reset();

            // Transfer mass from parcels to grid
            fluidGrid.TransferMass();

            // Solve for pressure
            fluidGrid.SolvePressure(solverIterations);
            fluidGrid.UpdateVelocities();
            
            // Update visualization
            fluidDrawer.Visualize();

            // Advection
            fluidGrid.AdvectVelocity();
        }

        void HandleInput() {
            fluidDrawer.HandleInteraction();

            if (Input.GetKeyDown(KeyCode.C)) {
                fluidGrid.ClearVelocities();
            }
        }
    }
}
