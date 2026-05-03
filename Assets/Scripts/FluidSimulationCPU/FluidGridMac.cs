using UnityEngine;
using static UnityEngine.Mathf;
using System;
using System.Linq;

namespace FluidSimulationCPU {
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
    public enum Axis { X, Y }

    /// <summary>
    /// Cell type: represents the content of the cell
    /// </summary>
    public enum CellType { Fluid, Solid }

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

    public readonly int width;
    public readonly int height;

    public readonly CellType[,] cellType;
    public readonly float[,] velocityU;
    public readonly float[,] velocityV;
    public readonly float[,] velocityUNext;
    public readonly float[,] velocityVNext;
    public readonly float[,] pressure;
    readonly PressureSolverData[,] pressureSolverData;

    public readonly float[,] temperature;
    public readonly float[,] temperatureNext;
    public readonly float[,] mass;
    public readonly float[,] massNext;
    public readonly float[,] massEdgeU;
    public readonly float[,] massEdgeV;

    /// <summary>
    /// Successive Over-Relaxation multiplier used for Gauss-Seidel
    /// pressure calculation
    /// </summary>
    public float SorMultiplier { get; set; }

    /// <summary>
    /// Fluid density. Assumed constant throughout all the fluid
    /// Density is in [kg/m^2]
    /// </summary>
    public float Density { get; set; }

    /// <summary>
    /// Ambient temperature in K
    /// </summary>
    public float AmbientTemperature { get; set; }

    /// <summary>
    /// Buoyancy formula smoke concentration multiplier
    /// </summary>
    public float SmokeBuoyancyMultiplier { get; set; }

    /// <summary>
    /// Buoyancy formula temperature multiplier
    /// </summary>
    public float TemperatureBuoyancyMultiplier { get; set; }

    public float MaxVelocity => Mathf.Max(
        velocityU.Cast<float>().Select(Mathf.Abs).Max(),
        velocityV.Cast<float>().Select(Mathf.Abs).Max()
    );

    public FluidGridMac(int width, int height) {
        this.width = width;
        this.height = height;

        SorMultiplier = 1.7f;
        Density = 1.3f;
        AmbientTemperature = 300f;
        SmokeBuoyancyMultiplier = 0.3f;
        TemperatureBuoyancyMultiplier = 1f;

        cellType = new CellType[width, height];
        velocityU = new float[width + 1, height];
        velocityV = new float[width, height + 1];
        velocityUNext = new float[width + 1, height];
        velocityVNext = new float[width, height + 1];
        pressure = new float[width, height];
        pressureSolverData = new PressureSolverData[width, height];

        temperature = new float[width, height];
        temperatureNext = new float[width, height];
        mass = new float[width, height];
        massNext = new float[width, height];
        massEdgeU = new float[width + 1, height];
        massEdgeV = new float[width, height + 1];

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
    /// Check if a given coordinate is within the Grid Cell centers bounds
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>True if the coordinates are within the bounds, false otherwise</returns>
    bool IsInCellCenterBounds(int i, int j) {
        return i >= 0 && i < width && j >= 0 && j < height;
    }

    /// <summary>
    /// Get a value fixed at the Cell center at a given coordinate inside the Grid
    /// If the coordinate are outside the Grid bounds the default value is returned
    /// </summary>
    /// <param name="grid">The array to take the values from</param>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <param name="defaultValue">Default value to return in case of failure</param>
    /// <returns>The value fixed at the Cell center of a given coordinate</returns>
    public T GetCellCenterValue<T>(T[,] grid, int i, int j, T defaultValue = default) {
        return IsInCellCenterBounds(i, j) ? grid[i, j] : defaultValue;
    }

    /// <summary>
    /// Set a value fixed at the Cell center at a given coordinate inside the Grid
    /// If the coordinate are outside the Grid bounds nothing happens
    /// </summary>
    /// <param name="grid">The array to take the values from</param>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <param name="val">The new value to set</param>
    public void SetCellCenterValue<T>(T[,] grid, int i, int j, T val) {
        if (IsInCellCenterBounds(i, j))
            grid[i, j] = val;
    }

    /// <summary>
    /// Get Cell Type given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid <code>CellType.Solid</code> is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Cell Type of the Cell at coordinate i and j</returns>
    public CellType GetCellType(int i, int j) {
        return GetCellCenterValue(cellType, i, j, CellType.Solid);
    }

    /// <summary>
    /// Get Cell pressure given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid <code>0</code> is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Pressure of the cell at coordinate i and j</returns>
    public float GetPressure(int i, int j) {
        return GetCellCenterValue(pressure, i, j, 0f);
    }

    /// <summary>
    /// Get Cell temperature given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid the ambient
    /// temperature is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Temperature of the cell at coordinate i and j</returns>
    public float GetTemperature(int i, int j) {
        return GetCellCenterValue(temperature, i, j, AmbientTemperature);
    }

    /// <summary>
    /// Get Cell smoke concentration given the coordinates inside the Grid
    /// If any of the coordinates are out of the bounds of the Grid <c>0</c>
    /// is returned
    /// </summary>
    /// <param name="i">Cell coordinate on the horizontal axis</param>
    /// <param name="j">Cell coordinate on the vertical axis</param>
    /// <returns>Temperature of the cell at coordinate i and j</returns>
    public float GetSmoke(int i, int j) {
        return GetCellCenterValue(mass, i, j, 0f);
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
    /// Transfer parcels data to the grid such as the mass and velocities
    /// </summary>
    /// <param name="parcels">The parcels object containing the data to transfer</param>
    public void TransferParcelsData(FluidParcels parcels) {
        TransferMass(parcels);
        TransferMomentum(parcels);
    }

    void TransferMass(FluidParcels parcels) {
        ClearSmoke();
        AccumulateCellCenterMass(parcels);
        AccumulateCellEdgesMass(parcels);
    }

    void AccumulateCellCenterMass(FluidParcels parcels) {
        for (int i = 0; i < parcels.count; ++i) {
            float m = parcels.mass[i];
            Vector2 p = parcels.position[i];

            int xCell = Mathf.FloorToInt(p.x);
            int yCell = Mathf.FloorToInt(p.y);
            float dx = p.x - xCell;
            float dy = p.y - yCell;

            int[] x = { xCell, xCell + 1, xCell, xCell + 1 };
            int[] y = { yCell, yCell, yCell + 1, yCell + 1 };
            float[] weights = {
                (1f - dx) * (1f - dy),
                (     dx) * (1f - dy),
                (1f - dx) * (     dy),
                (     dx) * (     dy)
            };
            for (int j = 0; j < 4; ++j) {
                if (IsInCellCenterBounds(x[j], y[j]))
                    mass[x[j], y[j]] += m * weights[j];
            }
        }
    }

    void AccumulateCellEdgesMass(FluidParcels parcels) {
        for (int i = 0; i < parcels.count; ++i) {
            float m = parcels.mass[i];
            Vector2 p = parcels.position[i];

            // Horizontal axis
            {
                int xEdge = Mathf.RoundToInt(p.x);
                int yCell = Mathf.FloorToInt(p.y);
                float xLeft = xEdge - 0.5f;

                float dx = p.x - xLeft;
                float dy = p.y - yCell;

                float[] x = { xLeft, xLeft + 1, xLeft, xLeft + 1 };
                float[] y = { yCell, yCell, yCell + 1, yCell + 1 };
                float[] weights = {
                    (1f - dx) * (1f - dy),
                    (     dx) * (1f - dy),
                    (1f - dx) * (     dy),
                    (     dx) * (     dy)
                };
                for (int j = 0; j < 4; ++j) {
                    float mass = GetVelocity(massEdgeU, x[j], y[j], Axis.X);
                    mass += m * weights[j];
                    SetVelocity(massEdgeU, x[j], y[j], Axis.X, mass);
                }
            }
            // Vertical axis
            {
                int xCell = Mathf.FloorToInt(p.x);
                int yEdge = Mathf.RoundToInt(p.y);
                float yBottom = yEdge - 0.5f;

                float dx = p.x - xCell;
                float dy = p.y - yBottom;

                float[] x = { xCell, xCell + 1, xCell, xCell + 1 };
                float[] y = { yBottom, yBottom, yBottom + 1, yBottom + 1 };
                float[] weights = {
                    (1f - dx) * (1f - dy),
                    (     dx) * (1f - dy),
                    (1f - dx) * (     dy),
                    (     dx) * (     dy)
                };
                for (int j = 0; j < 4; ++j) {
                    float mass = GetVelocity(massEdgeV, x[j], y[j], Axis.Y);
                    mass += m * weights[j];
                    SetVelocity(massEdgeV, x[j], y[j], Axis.Y, mass);
                }
            }
        }
    }

    void TransferMomentum(FluidParcels parcels) {
        ClearVelocities();
        for (int i = 0; i < parcels.count; ++i) {
            float m = parcels.mass[i];
            Vector2 v = parcels.velocity[i];
            Vector2 p = parcels.position[i];

            // Horizontal axis
            {
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.FloorToInt(p.y);

                float xLeft = x - 0.5f;
                float xRight = x + 0.5f;
                Vector2 cx = parcels.cx[i];

                Vector2 dpBottomLeft = new Vector2(xLeft - p.x, y - p.y);
                Vector2 dpBottomRight = new Vector2(xRight - p.x, y - p.y);
                Vector2 dpTopLeft = new Vector2(xLeft - p.x, y + 1 - p.y);
                Vector2 dpTopRight = new Vector2(xRight - p.x, y + 1 - p.y);

                float wBottomLeft  = (     dpTopRight.x) * (     dpTopRight.y);
                float wBottomRight = (1f - dpTopRight.x) * (     dpTopRight.y);
                float wTopLeft     = (     dpTopRight.x) * (1f - dpTopRight.y);
                float wTopRight    = (1f - dpTopRight.x) * (1f - dpTopRight.y);

                float vBottomLeft = GetVelocity(velocityU, xLeft, y, Axis.X);
                vBottomLeft += m * wBottomLeft * (v.x + Vector2.Dot(cx, dpBottomLeft));
                SetVelocity(velocityU, xLeft, y, Axis.X, vBottomLeft);

                float vBottomRight = GetVelocity(velocityU, xRight, y, Axis.X);
                vBottomRight += m * wBottomRight * (v.x + Vector2.Dot(cx, dpBottomRight));
                SetVelocity(velocityU, xRight, y, Axis.X, vBottomRight);

                float vTopLeft = GetVelocity(velocityU, xLeft, y + 1f, Axis.X);
                vTopLeft += m * wTopLeft * (v.x + Vector2.Dot(cx, dpTopLeft));
                SetVelocity(velocityU, xLeft, y + 1f, Axis.X, vTopLeft);

                float vTopRight = GetVelocity(velocityU, xRight, y + 1f, Axis.X);
                vTopRight += m * wTopRight * (v.x + Vector2.Dot(cx, dpTopRight));
                SetVelocity(velocityU, xRight, y + 1f, Axis.X, vTopRight);
            }

            // Vertical axis
            {
                int x = Mathf.FloorToInt(p.x);
                int y = Mathf.RoundToInt(p.y);

                float yBottom = y - 0.5f;
                float yTop = y + 0.5f;
                Vector2 cy = parcels.cy[i];

                Vector2 dpBottomLeft = new Vector2(x - p.x, yBottom - p.y);
                Vector2 dpBottomRight = new Vector2(x + 1 - p.x, yBottom - p.y);
                Vector2 dpTopLeft = new Vector2(x - p.x, yTop - p.y);
                Vector2 dpTopRight = new Vector2(x + 1 - p.x, yTop - p.y);

                float wBottomLeft  = (     dpTopRight.x) * (     dpTopRight.y);
                float wBottomRight = (1f - dpTopRight.x) * (     dpTopRight.y);
                float wTopLeft     = (     dpTopRight.x) * (1f - dpTopRight.y);
                float wTopRight    = (1f - dpTopRight.x) * (1f - dpTopRight.y);

                float vBottomLeft = GetVelocity(velocityV, x, yBottom, Axis.Y);
                vBottomLeft += m * wBottomLeft * (v.y + Vector2.Dot(cy, dpBottomLeft));
                SetVelocity(velocityV, x, yBottom, Axis.Y, vBottomLeft);

                float vBottomRight = GetVelocity(velocityV, x + 1f, yBottom, Axis.Y);
                vBottomRight += m * wBottomRight * (v.y + Vector2.Dot(cy, dpBottomRight));
                SetVelocity(velocityV, x + 1, yBottom, Axis.Y, vBottomRight);

                float vTopLeft = GetVelocity(velocityV, x, yTop, Axis.Y);
                vTopLeft += m * wTopLeft * (v.y + Vector2.Dot(cy, dpTopLeft));
                SetVelocity(velocityV, x, yTop, Axis.Y, vTopLeft);

                float vTopRight = GetVelocity(velocityV, x + 1, yTop, Axis.Y);
                vTopRight += m * wTopRight * (v.y + Vector2.Dot(cy, dpTopRight));
                SetVelocity(velocityV, x + 1, yTop, Axis.Y, vTopRight);
            }
        }
        TransferVelocities(parcels);
    }

    void TransferVelocities(FluidParcels parcels) {
        for (int j = 0; j < velocityU.GetLength(1); ++j) {
            for (int i = 0; i < velocityU.GetLength(0); ++i) {
                float x = i - 0.5f;
                float y = j;

                float m = GetVelocity(massEdgeU, x, y, Axis.X);
                if (m > 0) {
                    float u = GetVelocity(velocityU, x, y, Axis.X) / m;
                    SetVelocity(velocityU, x, y, Axis.X, u);
                }
            }
        }

        for (int i = 0; i < velocityV.GetLength(0); ++i) {
            for (int j = 0; j < velocityV.GetLength(1); ++j) {
                float x = i;
                float y = j - 0.5f;

                float m = GetVelocity(massEdgeV, x, y, Axis.Y);
                if (m > 0) {
                    float v = GetVelocity(velocityV, x, y, Axis.Y) / m;
                    SetVelocity(velocityV, x, y, Axis.Y, v);
                }
            }
        }
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
                bool isSolid = GetCellType(i, j) != CellType.Fluid;
                uint flowTop = isSolid || GetCellType(i, j + 1) != CellType.Fluid ? 0U : 1U;
                uint flowBottom = isSolid || GetCellType(i, j - 1) != CellType.Fluid ? 0U : 1U;
                uint flowRight = isSolid || GetCellType(i + 1, j) != CellType.Fluid ? 0U : 1U;
                uint flowLeft = isSolid || GetCellType(i - 1, j) != CellType.Fluid ? 0U : 1U;
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
                    float newPressure = (pressureSum - Density * info.velocityTerm) / (float)info.flowEdgeCount;
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
        float k = dt / Density;

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
    /// Add external forces to the entire body of fluid
    /// </summary>
    /// <param name="acceleration">The acceleration vectors to apply to the fluid<param>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AddExternalBodyForce(Vector2[] accelerations, float dt) {
        foreach (Vector2 a in accelerations) {
            // Horizontal
            for (int j = 0; j < velocityU.GetLength(1); ++j) {
                for (int i = 0; i < velocityU.GetLength(0); ++i) {
                    float x = i - 0.5f;
                    float y = j;

                    CellType rightType = GetCellType(i, j);
                    CellType leftType = GetCellType(i - 1, j);
                    if (leftType == CellType.Fluid || rightType == CellType.Fluid) {
                        float u = GetVelocity(velocityU, x, y, Axis.X);
                        u += a.x * dt;
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
                    if (bottomType == CellType.Fluid || topType == CellType.Fluid) {
                        float v = GetVelocity(velocityV, x, y, Axis.Y);
                        v += a.y * dt;
                        SetVelocity(velocityV, x, y, Axis.Y, v);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Add buoyancy force to the fluid
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AddBuoyancyForce(float dt) {
        // Vertical
        for (int i = 0; i < velocityV.GetLength(0); ++i) {
            for (int j = 0; j < velocityV.GetLength(1); ++j) {
                float x = i;
                float y = j - 0.5f;

                CellType topType = GetCellType(i, j);
                CellType bottomType = GetCellType(i, j - 1);
                if (bottomType == CellType.Fluid || topType == CellType.Fluid) {
                    float temperature = (GetTemperature(i, j) + GetTemperature(i, j - 1)) * 0.5f;
                    float smoke = (GetSmoke(i, j) + GetSmoke(i, j - 1)) * 0.5f;
                    float buoyancy = -SmokeBuoyancyMultiplier * smoke +
                                     TemperatureBuoyancyMultiplier * (temperature - AmbientTemperature);

                    float v = GetVelocity(velocityV, x, y, Axis.Y);
                    v += buoyancy * dt;
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
        float u = SampleBilinearCellEdges(velocityU, position, Axis.X);
        float v = SampleBilinearCellEdges(velocityV, position, Axis.Y);
        return new Vector2(u, v);
    }

    /// <summary>
    /// Sample a value at a given point inside the Grid interpolating values
    /// centered at the Cells edges
    /// </summary>
    /// <param name="map">Array of values to interpolate</param>
    /// <param name="position">Coordinates of the sample to take</param>
    /// <param name="axis">Grid axis to consider for the interpolation</param>
    /// <returns>The sampled value</returns>
    public float SampleBilinearCellEdges(float[,] map, Vector2 position, Axis axis) {
        float xOff = axis == Axis.X ? 0.5f : 0;
        float yOff = axis == Axis.Y ? 0.5f : 0;

        int w = map.GetLength(0);
        int h = map.GetLength(1);
        float x = Mathf.Clamp(position.x, 0, w - 2);
        float y = Mathf.Clamp(position.y, 0, h - 2);

        float left = Mathf.Floor(x + xOff) - xOff;
        float bottom = Mathf.Floor(y + yOff) - yOff;
        float right = left + 1f;
        float top = bottom + 1f;

        // Get values on the four edges
        float lt = GetVelocity(map, left, top, axis);
        float rt = GetVelocity(map, right, top, axis);
        float lb = GetVelocity(map, left, bottom, axis);
        float rb = GetVelocity(map, right, bottom, axis);

        // Calculate how far [0,1] the input point is along the current cell
        float xFrac = Clamp01(x - left);
        float yFrac = Clamp01(y - bottom);
        return Blerp(lb, rb, lt, rt, xFrac, yFrac);
    }

    /// <summary>
    /// Sample a value at a given point inside the Grid interpolating values
    /// centered at the Cells center
    /// </summary>
    /// <param name="map">Array of values to interpolate</param>
    /// <param name="position">Coordinates of the sample to take</param>
    /// <returns>The sampled value</returns>
    public float SampleBilinearCellCenter(float[,] map, Vector2 position) {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int left = Mathf.Clamp((int)position.x, 0, w - 2);
        int bottom = Mathf.Clamp((int)position.y, 0, h - 2);
        int right = left + 1;
        int top = bottom + 1;

        // Get values of the adjacent Cells
        float lt = map[left, top];
        float rt = map[right, top];
        float lb = map[left, bottom];
        float rb = map[right, bottom];

        // Calculate how far [0,1] the input point is along the current cell
        float xFrac = Clamp01(position.x - left);
        float yFrac = Clamp01(position.y - bottom);
        return Blerp(lb, rb, lt, rt, xFrac, yFrac);
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
    /// Advect temperature using the Semi-Lagrangian method
    /// In a Semi-Lagrangian method we can imagine a particle traveling at a
    /// certain velocity landing on the Cell center.
    /// Since we know the final position and velocity of the "virtual particle"
    /// via interpolation we can calculate its previous position given the
    /// simulation time step
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AdvectTemperature(float dt) {
        for (int i = 0; i < width; ++i) {
            for (int j = 0; j < height; ++j) {
                Vector2 position = new Vector2(i, j);
                Vector2 velocity = SampleVelocity(position);
                Vector2 positionPrev = position - velocity * dt;
                temperatureNext[i, j] = SampleBilinearCellCenter(temperature, positionPrev);
            }
        }

        Array.Copy(temperatureNext, temperature, temperature.Length);
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
                massNext[i, j] = SampleBilinearCellCenter(mass, positionPrev);
            }
        }

        Array.Copy(massNext, mass, mass.Length);
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
                        mass[i, j] += amount * fallof;
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
        Array.Clear(mass, 0, mass.Length);
        Array.Clear(massEdgeU, 0, massEdgeU.Length);
        Array.Clear(massEdgeV, 0, massEdgeV.Length);
    }

    /// <summary>
    /// Clear all temperature values setting them to the ambient temperature
    /// </summary>
    public void ClearTemperature() {
        for (int i = 0; i < temperature.GetLength(0); ++i)
            for (int j = 0; j < temperature.GetLength(1); ++j)
                temperature[i, j] = AmbientTemperature;
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
