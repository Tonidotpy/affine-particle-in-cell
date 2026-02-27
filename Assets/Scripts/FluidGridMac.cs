using UnityEngine;
using static UnityEngine.Mathf;
using System;

namespace FluidSimulation {
/// <summary>
/// Representation of a bidimensional Staggered Grid containing a fluid
/// discretize into mutliple Cells.
/// The Grid has a specific width and height, which defines the total number of Cells.
///
/// Each Cell is asserted to be a square of size 1 with its pressure values
/// located at its center while the horizontal and vertical velocity components
/// are located on the Cell edges.
///
/// To simplify data access the same coordinate system is used with specific
/// functions to change reference system or map indices to coordinates whenever
/// necessary.
/// The coordinate system has its origin at the bottom left Cell center, the
/// horizontal (X) coordinates increases to the right and the vertical (Y)
/// coordinates increases to the top.
/// The distance between two Cell centers is 1 and the distance from the center
/// of a Cell to any of its edges is 0.5
/// </summary>
public class FluidGridMac {
    /// <summary>
    /// Grid axis: X is horizontal, Y is vertical
    /// </summary>
    public enum Axis {
        X,
        Y
    }

    /// <summary>
    /// Cell type: represents the content of the cell
    /// </summary>
    public enum CellType {
        Fluid,
        Solid,
    }

    /// <summary>
    /// Data needed to solve the pressure equation using the Gauss-Seidel method
    /// </summary>
    struct PressureSolverData {
        // flowData is made out of multiple variables compressed into a single
        // integer for optimization purposes
        //
        // The format is the following:
        //     [31     ] - flowTop: 0 if there is no flow from the top cell 1 otherwise
        //     [30     ] - flowBottom: 0 if there is no flow from the bottom cell 1 otherwise
        //     [29     ] - flowRight: 0 if there is no flow from the right cell 1 otherwise
        //     [28     ] - flowLeft: 0 if there is no flow from the top cell 1 otherwise
        //     [27 -  8] - Reserved
        //     [ 7 -  0] - flowEdgeCount: number of edges with flows different from 0
        public uint flowData;
        public float velocityTerm;

        public uint flowTop {
            get { return (flowData >> 31) & 1U; }
            set {
                flowData &= ~(1U << 31);
                flowData |= (value == 0 ? 0U : 1U) << 31;
            }
        }
        public uint flowBottom {
            get { return (flowData >> 30) & 1U; }
            set {
                flowData &= ~(1U << 30);
                flowData |= (value == 0 ? 0U : 1U) << 30;
            }
        }
        public uint flowRight {
            get { return (flowData >> 29) & 1U; }
            set {
                flowData &= ~(1U << 29);
                flowData |= (value == 0 ? 0U : 1U) << 29;
            }
        }
        public uint flowLeft {
            get { return (flowData >> 28) & 1U; }
            set {
                flowData &= ~(1U << 28);
                flowData |= (value == 0 ? 0U : 1U) << 28;
            }
        }
        public uint flowEdgeCount {
            get { return flowData & 0xFF; }
            set {
                flowData &= 0xFFFFFF00;
                flowData |= value & 0xFF;
            }
        }
    }

    private const float density = 1f; // g/ml

    public readonly int width;
    public readonly int height;

    public readonly CellType[,] cellType;
    public readonly float[,] velocityU;
    public readonly float[,] velocityV;
    public readonly float[,] velocityUNext;
    public readonly float[,] velocityVNext;
    public readonly float[,] pressure;
    readonly PressureSolverData[,] pressureSolverData;

    public readonly float[,] smokeMap;
    public readonly float[,] smokeMapNext;

    /// <summary>
    /// Successive Over-Relaxation multiplier used for Gauss-Seidel
    /// pressure calculation
    /// </summary>
    public float SorMultiplier { get; set; }

    public FluidGridMac(int width, int height) {
        this.width = width;
        this.height = height;

        SorMultiplier = 1.7f;

        cellType = new CellType[width, height];
        velocityU = new float[width + 1, height];
        velocityV = new float[width, height + 1];
        velocityUNext = new float[width + 1, height];
        velocityVNext = new float[width, height + 1];
        pressure = new float[width, height];
        pressureSolverData = new PressureSolverData[width, height];

        smokeMap = new float[width, height];
        smokeMapNext = new float[width, height];

        SetExternalBoundaries();
    }

    /// <summary>
    /// Set Grid boundaries as solid Cells
    /// </summary>
    void SetExternalBoundaries() {
        // Treat borders as solid
        for (int i = 0; i < width; ++i) {
            cellType[i, 0] = CellType.Solid;
            cellType[i, height - 1] = CellType.Solid;
        }
        for (int j = 0; j < height; ++j) {
            cellType[0, j] = CellType.Solid;
            cellType[width - 1, j] = CellType.Solid;
        }
    }

    /// <summary>
    /// Get Cell Type given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid <code>CellType.Solid</code> is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Cell Type of the Cell at coordinate i and j</returns>
    public CellType GetCellType(int i, int j) {
        return (i < 0 || i >= width || j < 0 || j >= height) ? CellType.Solid : cellType[i, j];
    }

    /// <summary>
    /// Set Cell Type given the coordinates inside the Grid
    /// Cell Type is not set if any of the coordinates are out of the bounds of the Grid
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <param name="type">New Cell Type to set</param>
    public void SetCellType(int i, int j, CellType type) {
        if (i < 0 || i >= width || j < 0 || j >= height)
            return;
        cellType[i, j] = type;
    }

    /// <summary>
    /// Get Cell velocity on a given axis staggered Grid provided its
    /// coordinates in the main Grid
    /// The coordinates are mapped from the main Grid reference coordinate
    /// system to the staggered Grid coordinate system
    /// If any of the coordinates are out of the bounds of the Grid
    /// <code>0</code> is returned
    /// </summary>
    /// <param name="velocity">Array of velocities</param>
    /// <param name="x">Cell coordinate on the horizontal axis</param>
    /// <param name="y">Cell coordinate on the vertical axis</param>
    /// <param name="axis">Axis to consider</param>
    /// <returns>Velocity of the staggered Grid Cell at coordinate x and y</returns>
    public float GetVelocity(float[,] velocity, float x, float y, Axis axis) {
        float xOff = axis == Axis.X ? 0.5f : 0;
        float yOff = axis == Axis.Y ? 0.5f : 0;
        int i = Mathf.RoundToInt(x + xOff);
        int j = Mathf.RoundToInt(y + yOff);

        bool xBoundsCheck = axis == Axis.X && (i >= 0 && i < width + 1 && j >= 0 && j < height);
        bool yBoundsCheck = axis == Axis.Y && (i >= 0 && i < width && j >= 0 && j < height + 1);
        return (xBoundsCheck || yBoundsCheck) ? velocity[i, j] : 0;
    }

    /// <summary>
    /// Set Cell velocity on a given axis staggered Grid provided its
    /// coordinates in the main Grid
    /// The coordinates are mapped from the main Grid reference coordinate
    /// system to the staggered Grid coordinate system
    /// If any of the coordinates are out of the bounds of the Grid
    /// the value is not set
    /// </summary>
    /// <param name="velocity">Array of velocities</param>
    /// <param name="x">Cell coordinate on the horizontal axis</param>
    /// <param name="y">Cell coordinate on the vertical axis</param>
    /// <param name="axis">Axis to consider</param>
    /// <param name="newVelocity">New velocity value to set</param>
    public void SetVelocity(float[,] velocity, float x, float y, Axis axis, float newVelocity) {
        float xOff = axis == Axis.X ? 0.5f : 0;
        float yOff = axis == Axis.Y ? 0.5f : 0;
        int i = Mathf.RoundToInt(x + xOff);
        int j = Mathf.RoundToInt(y + yOff);

        bool xBoundsCheck = axis == Axis.X && (i >= 0 && i < width + 1 && j >= 0 && j < height);
        bool yBoundsCheck = axis == Axis.Y && (i >= 0 && i < width && j >= 0 && j < height + 1);
        if (xBoundsCheck || yBoundsCheck)
            velocity[i, j] = newVelocity;
    }

    /// <summary>
    /// Get Cell pressure given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid <code>0</code> is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Pressure of the cell at coordinate i and j</returns>
    public float GetPressure(int i, int j) {
        return (i < 0 || i >= width || j < 0 || j >= height) ? 0f : pressure[i, j];
    }

    /// <summary>
    /// Calculate pressure values needed to remove divergence of fluid
    /// using the Gauss-Seidel method with SOR
    /// </summary>
    /// <param name="iterations">Total number of iterations of the pressure solver</param>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void SolvePressure(int iterations, float dt) {
        PreparePressureSolver(dt);

        for (int i = 0; i < iterations; ++i) {
            RunPressureSolver();
        }
    }

    /// <summary>
    /// Pre-calculate values needed by the pressure solver to increase performance
    /// of the solver
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    void PreparePressureSolver(float dt) {
        for (int i = 0; i < width; ++i) {
            for (int j = 0; j < height; ++j) {
                uint flowTop = GetCellType(i, j + 1) != CellType.Fluid ? 0U : 1U;
                uint flowBottom = GetCellType(i, j - 1) != CellType.Fluid ? 0U : 1U;
                uint flowRight = GetCellType(i + 1, j) != CellType.Fluid ? 0U : 1U;
                uint flowLeft = GetCellType(i - 1, j) != CellType.Fluid ? 0U : 1U;
                uint flowEdgeCount = flowTop + flowBottom + flowRight + flowLeft;

                float velocityTop = GetVelocity(velocityV, i, j + 0.5f, Axis.Y);
                float velocityBottom = GetVelocity(velocityV, i, j - 0.5f, Axis.Y);
                float velocityRight = GetVelocity(velocityU, i + 0.5f, j, Axis.X);
                float velocityLeft = GetVelocity(velocityU, i - 0.5f, j, Axis.X);
                float velocityTerm = (velocityTop - velocityBottom + velocityRight - velocityLeft) / dt;

                pressureSolverData[i, j] = new PressureSolverData() {
                    flowTop = flowTop,
                    flowBottom = flowBottom,
                    flowRight = flowRight,
                    flowLeft = flowLeft,
                    flowEdgeCount = flowEdgeCount,
                    velocityTerm = velocityTerm
                };
            }
        }
    }

    /// <summary>
    /// Run the Gauss-Seidel method to solve the pressure equation
    /// </summary>
    void RunPressureSolver() {
        for (int i = 0; i < width; ++i) {
            for (int j = 0; j < height; ++j) {
                PressureSolverData info = pressureSolverData[i, j];

                // Consider only fluid cells with at least one flow on their edges
                if (cellType[i, j] == CellType.Fluid && info.flowEdgeCount != 0) {
                    // Get pressure contribution of neighbors cells
                    float pressureTop = GetPressure(i, j + 1) * info.flowTop;
                    float pressureBottom = GetPressure(i, j - 1) * info.flowBottom;
                    float pressureRight = GetPressure(i + 1, j) * info.flowRight;
                    float pressureLeft = GetPressure(i - 1, j) * info.flowLeft;
                    float pressureSum = pressureTop + pressureBottom + pressureRight + pressureLeft;

                    // Calculate pressure
                    float newPressure = (pressureSum - density * info.velocityTerm) / (float)info.flowEdgeCount;
                    float oldPressure = pressure[i, j];
                    pressure[i, j] = oldPressure + (newPressure - oldPressure) * SorMultiplier;
                } else {
                    pressure[i, j] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Correct velocity components based on the pressure difference between Grid Cells
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void UpdateVelocities(float dt) {
        float k = dt / density;

        // Horizontal
        for (int j = 0; j < velocityU.GetLength(1); ++j) {
            for (int i = 0; i < velocityU.GetLength(0); ++i) {
                float x = i - 0.5f;
                float y = j;

                CellType rightType = GetCellType(i, j);
                CellType leftType = GetCellType(i - 1, j);
                if (leftType != CellType.Fluid || rightType != CellType.Fluid) {
                    SetVelocity(velocityU, x, y, Axis.X, 0);
                } else {
                    float pressureRight = GetPressure(i, j);
                    float pressureLeft = GetPressure(i - 1, j);
                    float u = GetVelocity(velocityU, x, y, Axis.X) - k * (pressureRight - pressureLeft);
                    SetVelocity(velocityU, x, y, Axis.X, u);
                }
            }
        }

        // Vertical
        for (int i = 0; i < velocityV.GetLength(0); ++i) {
            for (int j = 0; j < velocityV.GetLength(1); ++j) {
                float x = i;
                float y = j - 0.5f;

                CellType topType = GetCellType(i, j);
                CellType bottomType = GetCellType(i, j - 1);
                if (bottomType != CellType.Fluid || topType != CellType.Fluid) {
                    SetVelocity(velocityV, x, y, Axis.Y, 0);
                } else {
                    float pressureTop = GetPressure(i, j);
                    float pressureBottom = GetPressure(i, j - 1);
                    float v = GetVelocity(velocityV, x, y, Axis.Y) - k * (pressureTop - pressureBottom);
                    SetVelocity(velocityV, x, y, Axis.Y, v);
                }
            }
        }
    }

    /// <summary>
    /// Advect velocities using the Semi-Lagrangian method
    /// In a Semi-Lagrangian method we can imagine a particle traveling at a
    /// certain velocity landing on the Cell borders.
    /// Since we know the final position and velocity of the "virtual particle"
    /// via interpolation we can calculate its previous position given the
    /// simulation time step
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AdvectVelocities(float dt) {
        // In the case of a fluid Cell near a solid Cell it may be considered
        // to use the Semi-Lagrangian method by assuming the "virtual particle"
        // to have an elastic collision on the solid face thus tracking back
        // its position
        // This may further reduce energy dissipation of the fluid due to
        // approximations

        // Horizontal
        for (int j = 0; j < velocityU.GetLength(1); ++j) {
            for (int i = 0; i < velocityU.GetLength(0); ++i) {
                float x = i - 0.5f;
                float y = j;

                CellType rightType = GetCellType(i, j);
                CellType leftType = GetCellType(i - 1, j);
                if (leftType != CellType.Fluid || rightType != CellType.Fluid) {
                    float u = GetVelocity(velocityU, x, y, Axis.X);
                    SetVelocity(velocityUNext, x, y, Axis.X, u);
                } else {
                    Vector2 position = new Vector2(x, y);
                    Vector2 velocity = SampleVelocity(position);
                    Vector2 positionPrev = position - velocity * dt;
                    float u = SampleVelocity(positionPrev).x;
                    SetVelocity(velocityUNext, x, y, Axis.X, u);
                }
            }
        }

        // Vertical
        for (int i = 0; i < velocityV.GetLength(0); ++i) {
            for (int j = 0; j < velocityV.GetLength(1); ++j) {
                float x = i;
                float y = j - 0.5f;

                CellType topType = GetCellType(i, j);
                CellType bottomType = GetCellType(i, j - 1);
                if (bottomType != CellType.Fluid || topType != CellType.Fluid) {
                    float v = GetVelocity(velocityV, x, y, Axis.Y);
                    SetVelocity(velocityVNext, x, y, Axis.Y, v);
                } else {
                    Vector2 position = new Vector2(x, y);
                    Vector2 velocity = SampleVelocity(position);
                    Vector2 positionPrev = position - velocity * dt;
                    float v = SampleVelocity(positionPrev).y;
                    SetVelocity(velocityVNext, x, y, Axis.Y, v);
                }
            }
        }

        // Update velocities
        Array.Copy(velocityUNext, velocityU, velocityU.Length);
        Array.Copy(velocityVNext, velocityV, velocityV.Length);
    }

    /// <summary>
    /// Sample a velocity vector at a given point in the Grid
    /// </summary>
    /// <param name="position">Coordinates of the sample to take</param>
    /// <returns>The sampled velocity vector</returns>
    public Vector2 SampleVelocity(Vector2 position) {
        float u = SampleBilinearVelocity(velocityU, position, Axis.X);
        float v = SampleBilinearVelocity(velocityV, position, Axis.Y);
        return new Vector2(u, v);
    }

    /// <summary>
    /// Sample velocity at a given point inside the Grid using bilinear interpolation
    /// </summary>
    /// <param name="velocity">Array of velocities</param>
    /// <param name="position">Coordinates of the sample to take</param>
    /// <param name="axis">Axis of the staggered grid to consider</param>
    /// <returns>The sampled velocity on a given axis</returns>
    public float SampleBilinearVelocity(float[,] velocity, Vector2 position, Axis axis) {
        float xOff = axis == Axis.X ? 0.5f : 0;
        float yOff = axis == Axis.Y ? 0.5f : 0;

        int w = velocity.GetLength(0);
        int h = velocity.GetLength(1);
        float x = Mathf.Clamp(position.x, 0, w - 2);
        float y = Mathf.Clamp(position.y, 0, h - 2);

        float left = Mathf.Floor(x + xOff) - xOff;
        float bottom = Mathf.Floor(y + yOff) - yOff;
        float right = left + 1f;
        float top = bottom + 1f;

        // Get velocities on the four edges
        float ltVelocity = GetVelocity(velocity, left, top, axis);
        float rtVelocity = GetVelocity(velocity, right, top, axis);
        float lbVelocity = GetVelocity(velocity, left, bottom, axis);
        float rbVelocity = GetVelocity(velocity, right, bottom, axis);

        // Calculate how far [0,1] the input point is along the current cell
        float xFrac = Clamp01(x - left);
        float yFrac = Clamp01(y - bottom);
        return Blerp(lbVelocity, rbVelocity, ltVelocity, rtVelocity, xFrac, yFrac);
    }

    /// <summary>
    /// Sample smoke at a given point inside the Grid using bilinear interpolation
    /// </summary>
    /// <param name="smoke">Array of smoke values</param>
    /// <param name="position">Coordinates of the sample to take</param>
    /// <returns>The sampled smoke value</returns>
    public float SampleBilinearSmoke(float[,] smoke, Vector2 position) {
        int w = smoke.GetLength(0);
        int h = smoke.GetLength(1);
        int left = Mathf.Clamp((int)position.x, 0, w - 2);
        int bottom = Mathf.Clamp((int)position.y, 0, h - 2);
        int right = left + 1;
        int top = bottom + 1;

        // Get smoke values of the adjacent Cells
        float ltSmoke = smokeMap[left, top];
        float rtSmoke = smokeMap[right, top];
        float lbSmoke = smokeMap[left, bottom];
        float rbSmoke = smokeMap[right, bottom];

        // Calculate how far [0,1] the input point is along the current cell
        float xFrac = Clamp01(position.x - left);
        float yFrac = Clamp01(position.y - bottom);
        return Blerp(lbSmoke, rbSmoke, ltSmoke, rtSmoke, xFrac, yFrac);
    }

    /// <summary>
    /// Bilinear interpolation of four values
    /// It can be imagined a square with the four values on the vertices
    /// and any value inside the square can be sampled
    /// </summary>
    /// <param name="lbValue">Value of the bottom left vertex</param>
    /// <param name="rbValue">Value of the bottom right vertex</param>
    /// <param name="ltValue">Value of the top left vertex</param>
    /// <param name="rtValue">Value of the top right vertex</param>
    /// <param name="xFrac">Horizontal coordinate of the sample to take from 0 to 1</param>
    /// <param name="yFrac">Vertical coordinate of the sample to take from 0 to 1</param>
    static float Blerp(float lbValue, float rbValue, float ltValue, float rtValue, float xFrac, float yFrac) {
        float lerpBottom = Lerp(lbValue, rbValue, xFrac);
        float lerpTop = Lerp(ltValue, rtValue, xFrac);
        return Lerp(lerpBottom, lerpTop, yFrac);
    }

    /// <summary>
    /// Advect smoke using the Semi-Lagrangian method
    /// In a Semi-Lagrangian method we can imagine a particle traveling at a
    /// certain velocity landing on the Cell center.
    /// Since we know the final position and velocity of the "virtual particle"
    /// via interpolation we can calculate its previous position given the
    /// simulation time step
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AdvectSmoke(float dt) {
        for (int i = 0; i < width; ++i) {
            for (int j = 0; j < height; ++j) {
                Vector2 position = new Vector2(i, j);
                Vector2 velocity = SampleVelocity(position);
                Vector2 positionPrev = position - velocity * dt;
                smokeMapNext[i, j] = SampleBilinearSmoke(smokeMap, positionPrev);
            }
        }

        Array.Copy(smokeMapNext, smokeMap, smokeMap.Length);
    }

    /// <summary>
    /// Add a specific amount of smoke at a given position inside a circle in the Grid
    /// </summary>
    /// <param name="position">Coordinates where the smoke is added</param>
    /// <param name="amount">Amount of smoke to add</param>
    /// <param name="radius">Radius of the circle where the smoke is added</param>
    public void AddSmokeAtPosition(Vector2 position, float amount, float radius) {
        int r = Mathf.CeilToInt(radius);
        for (int di = -r; di <= r; ++di) {
            for (int dj = -r; dj <= r; ++dj) {
                int i = (int)position.x + di;
                int j = (int)position.y + dj;

                // Add smoke in a circle only if the Cell is a fluid
                if (GetCellType(i, j) == CellType.Fluid) {
                    Vector2 center = new Vector2(i, j);
                    float distance = Vector2.Distance(position, center);
                    if (distance <= radius) {
                        float fallof = 1f - (distance / radius);
                        smokeMap[i, j] += amount * fallof;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clear all velocitie values setting them to 0
    /// </summary>
    public void ClearVelocities() {
        Array.Clear(velocityU, 0, velocityU.Length);
        Array.Clear(velocityV, 0, velocityV.Length);
    }

    /// <summary>
    /// Clear all pressure values setting them to 0
    /// </summary>
    public void ClearPressure() {
        Array.Clear(pressure, 0, pressure.Length);
    }

    /// <summary>
    /// Clear all smoke values setting them to 0
    /// </summary>
    public void ClearSmoke() {
        Array.Clear(smokeMap, 0, smokeMap.Length);
    }

    /// <summary>
    /// Clear all obstacles
    /// This sets all Cell types (except the borders) to Fluid
    /// </summary>
    public void ClearObstacles() {
        Array.Clear(cellType, (int)CellType.Fluid, cellType.Length);
        SetExternalBoundaries();
    }

    /// <summary>
    /// Calculate divergence of velocities at a specific cell
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    public float CalculateDivergenceAtCell(int i, int j) {
        float velocityRight = GetVelocity(velocityU, i + 0.5f, j, Axis.X);
        float velocityLeft = GetVelocity(velocityU, i - 0.5f, j, Axis.X);
        float velocityTop = GetVelocity(velocityV, i, j + 0.5f, Axis.Y);
        float velocityBottom = GetVelocity(velocityV, i, j - 0.5f, Axis.Y);

        float gradientX = velocityRight - velocityLeft;
        float gradientY = velocityTop - velocityBottom;
        return gradientX + gradientY;
    }
}
}
