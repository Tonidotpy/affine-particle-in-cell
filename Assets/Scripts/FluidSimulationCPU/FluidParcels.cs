using UnityEngine;
using System;
using System.Collections.Generic;

namespace FluidSimulationCPU {
/// <summary>
/// Representation of clusters of particles sharing the same state and behaviors.
/// Each parcel may contain multiple particles having the same position and velocity
/// </summary>
public class FluidParcels {
    public int count;

    public float[] mass;
    public Vector2[] position;
    public Vector2[] velocity;
    public float[] temperature;

    // Affine State vectors
    public Vector2[] cx;
    public Vector2[] cy;

    public FluidParcels(int count) {
        this.count = count;
        mass = new float[count];
        position = new Vector2[count];
        velocity = new Vector2[count];
        temperature = new float[count];
        cx = new Vector2[count];
        cy = new Vector2[count];
    }

    public void RemoveParcel(int index) {
        if (index < 0 || index >= count) return;

        int lastIndex = count - 1;
        if (index < lastIndex) {
            mass[index] = mass[lastIndex];
            velocity[index] = velocity[lastIndex];
            position[index] = position[lastIndex];
            temperature[index] = temperature[lastIndex];
            cx[index] = cx[lastIndex];
            cy[index] = cy[lastIndex];
        }
        --count;
    }

    public void AddParcel(FluidGridMac grid, Vector2 initalPosition, Vector2 initialVelocity) {
        Vector2Int cellPosition = new Vector2Int(
            Mathf.FloorToInt(initalPosition.x + 0.5f),
            Mathf.FloorToInt(initalPosition.y + 0.5f)
        );
        if (grid.GetCellType(cellPosition.x, cellPosition.y) == FluidGridMac.CellType.Solid)
            return;


        int capacity = mass.Length;
        if (count >= capacity) {
            Array.Resize(ref mass, capacity * 2);
            Array.Resize(ref position, capacity * 2);
            Array.Resize(ref velocity, capacity * 2);
            Array.Resize(ref temperature, capacity * 2);
            Array.Resize(ref cx, capacity * 2);
            Array.Resize(ref cy, capacity * 2);
        }

        mass[count] = 1f;
        velocity[count] = initialVelocity;
        position[count] = initalPosition;
        temperature[count] = grid.AmbientTemperature;
        cx[count] = Vector2.zero;
        cy[count] = Vector2.zero;
        ++count;
    }

    /// <summary>
    /// Transfer data from the MAC Grid to the Parcels
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    public void TransferGridData(FluidGridMac grid) {
        TransferVelocities(grid);
        // DEPRECATED: Temperature transfer
        // TransferTemperature(grid);
    }

    /// <summary>
    /// Transfer velocities from the MAC Grid to the Parcels
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    void TransferVelocities(FluidGridMac grid) {
        for (int i = 0; i < count; ++i) {
            Vector2 v = Vector2.zero;
            Vector2 p = position[i];

            // Horizontal axis
            {
                int xEdge = Mathf.RoundToInt(p.x);
                int yCell = Mathf.FloorToInt(p.y);

                float xLeft = xEdge - 0.5f;
                float[] x = { xLeft, xLeft + 1, xLeft, xLeft + 1 };
                float[] y = { yCell, yCell, yCell + 1, yCell + 1 };

                float xFrac = p.x - xLeft;
                float yFrac = p.y - yCell;
                float[] weight = {
                    (1f - xFrac) * (1f - yFrac),
                    (     xFrac) * (1f - yFrac),
                    (1f - xFrac) * (     yFrac),
                    (     xFrac) * (     yFrac)
                };
                for (int j = 0; j < 4; ++j) {
                    float velocity = grid.GetCellEdgeValue(grid.velocityU, x[j], y[j], FluidGridMac.Axis.X);
                    v.x += weight[j] * velocity;
                }
            }
            // Vertical axis
            {
                int xCell = Mathf.FloorToInt(p.x);
                int yEdge = Mathf.RoundToInt(p.y);

                float yBottom = yEdge - 0.5f;
                float[] x = { xCell, xCell + 1, xCell, xCell + 1 };
                float[] y = { yBottom, yBottom, yBottom + 1, yBottom + 1 };

                float xFrac = p.x - xCell;
                float yFrac = p.y - yBottom;
                float[] weight = {
                    (1f - xFrac) * (1f - yFrac),
                    (     xFrac) * (1f - yFrac),
                    (1f - xFrac) * (     yFrac),
                    (     xFrac) * (     yFrac)
                };
                for (int j = 0; j < 4; ++j) {
                    float velocity = grid.GetCellEdgeValue(grid.velocityV, x[j], y[j], FluidGridMac.Axis.Y);
                    v.y += weight[j] * velocity;
                }
            }
            velocity[i] = v;
        }
    }

    /// <summary>
    /// Transfer temperature values from the MAC Grid to the Parcels
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    void TransferTemperature(FluidGridMac grid) {
        for (int i = 0; i < count; ++i) {
            Vector2 p = position[i];
            Vector2Int cellPosition = new Vector2Int(
                Mathf.FloorToInt(p.x + 0.5f),
                Mathf.FloorToInt(p.y + 0.5f)
            );

            temperature[i] = grid.GetTemperature(cellPosition.x, cellPosition.y);
        }
    }

    /// <summary>
    /// Update the Affine State vectors based on Grid velocities
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    public void UpdateAffineState(FluidGridMac grid) {
        for (int i = 0; i < count; ++i) {
            Vector2 p = position[i];

            // Horizontal axis
            {
                int xEdge = Mathf.RoundToInt(p.x);
                int yCell = Mathf.FloorToInt(p.y);

                float xLeft = xEdge - 0.5f;
                float[] x = { xLeft, xLeft + 1, xLeft, xLeft + 1 };
                float[] y = { yCell, yCell, yCell + 1, yCell + 1 };

                float xFrac = p.x - xLeft;
                float yFrac = p.y - yCell;
                Vector2[] weightGradient = {
                    new Vector2(yFrac - 1f, xFrac - 1f),
                    new Vector2(1f - yFrac,    - xFrac),
                    new Vector2(   - yFrac, 1f - xFrac),
                    new Vector2(     yFrac,      xFrac)
                };

                Vector2 c = Vector2.zero;
                for (int j = 0; j < 4; ++j) {
                    float velocity = grid.GetCellEdgeValue(grid.velocityU, x[j], y[j], FluidGridMac.Axis.X);
                    c += weightGradient[j] * velocity;
                }
                cx[i] = c;
            }
            // Vertical axis
            {
                int xCell = Mathf.FloorToInt(p.x);
                int yEdge = Mathf.RoundToInt(p.y);

                float yBottom = yEdge - 0.5f;
                float[] x = { xCell, xCell + 1, xCell, xCell + 1 };
                float[] y = { yBottom, yBottom, yBottom + 1, yBottom + 1 };

                float xFrac = p.x - xCell;
                float yFrac = p.y - yBottom;
                Vector2[] weightGradient = {
                    new Vector2(yFrac - 1f, xFrac - 1f),
                    new Vector2(1f - yFrac,    - xFrac),
                    new Vector2(   - yFrac, 1f - xFrac),
                    new Vector2(     yFrac,      xFrac)
                };
                Vector2 c = Vector2.zero;
                for (int j = 0; j < 4; ++j) {
                    float velocity = grid.GetCellEdgeValue(grid.velocityV, x[j], y[j], FluidGridMac.Axis.Y);
                    c += weightGradient[j] * velocity;
                }
                cy[i] = c;
            }
        }
    }

    /// <summary>
    /// Advect Parcels inside the fluid
    /// </summary>
    /// <param name="grid">The MAC Grid to get</param>
    /// <param name="dt">The timestep</param>
    public void Advect(FluidGridMac grid, float dt) {
        for (int i = 0; i < count; ++i) {
            Vector2 p = position[i];
            Vector2 k1 = velocity[i];
            Vector2 k2 = grid.SampleVelocity(p + k1 * (dt * 0.5f));
            Vector2 k3 = grid.SampleVelocity(p + k2 * (dt * 0.5f));
            Vector2 k4 = grid.SampleVelocity(p + k3 * dt);

            // RK4 implementation
            Vector2 dp = (dt / 6f) * (k1 + 2 * k2 + 2 * k3 + k4);

            if (dp == Vector2.zero) continue;
            // This adds dissipation but simplify collisions but it does not
            // happen that frequently. The magnitude usually seats between 0 and 1
            dp = Vector2.ClampMagnitude(dp, 1f);

            Vector2 positionNext = p + dp;

            Vector2Int cellPosition = new Vector2Int(
                Mathf.FloorToInt(positionNext.x + 0.5f),
                Mathf.FloorToInt(positionNext.y + 0.5f)
            );
            if (grid.GetCellType(cellPosition.x, cellPosition.y) == FluidGridMac.CellType.Solid) {
                Vector2Int cellHorizontalNext = new Vector2Int(cellPosition.x, Mathf.FloorToInt(p.y + 0.5f));
                Vector2Int cellVerticalNext = new Vector2Int(Mathf.FloorToInt(p.x + 0.5f), cellPosition.y);

                bool isHorizontalSolid = grid.GetCellType(cellHorizontalNext.x, cellHorizontalNext.y) == FluidGridMac.CellType.Solid;
                bool isVerticalSolid = grid.GetCellType(cellVerticalNext.x, cellVerticalNext.y) == FluidGridMac.CellType.Solid;

                if (isHorizontalSolid) {
                    velocity[i].x = 0;
                    positionNext.x = p.x;
                }
                if (isVerticalSolid) {
                    velocity[i].y = 0;
                    positionNext.y = p.y;
                }

                if (!isHorizontalSolid && !isVerticalSolid) {
                    if (Mathf.Abs(dp.x) < Mathf.Abs(dp.y)) {
                        velocity[i].x = 0;
                        positionNext.x = p.x;
                    }
                    else {
                        velocity[i].y = 0;
                        positionNext.y = p.y;
                    }
                }

                Vector2Int cellFinalPosition = new Vector2Int(
                    Mathf.FloorToInt(positionNext.x + 0.5f),
                    Mathf.FloorToInt(positionNext.y + 0.5f)
                );
                if (grid.GetCellType(cellFinalPosition.x, cellFinalPosition.y) == FluidGridMac.CellType.Solid) {
                    positionNext = p;
                    velocity[i] = Vector2.zero;
                }
            }
            position[i] = positionNext;
            if (position[i].x <= -1f || position[i].x >= grid.width + 1f ||
                position[i].y <= -1f || position[i].y >= grid.height + 1f) {
                RemoveParcel(i);
                --i; // Avoid skipping a particle
            }
        }
    }
}
}
