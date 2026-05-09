using UnityEngine;
using System;

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

    // Affine State vectors
    public Vector2[] cx;
    public Vector2[] cy;

    public FluidParcels(int count) {
        this.count = count;
        mass = new float[count];
        position = new Vector2[count];
        velocity = new Vector2[count];
        cx = new Vector2[count];
        cy = new Vector2[count];

        for (int i = 0; i < count; ++i) {
            mass[i] = 1f;
            position[i] = new Vector2(Mathf.Cos(i), Mathf.Sin(i)) + Vector2.one * 5f;
        }
    }

    public void RemoveParcel(int index) {
        if (index < 0 || index >= count) return;

        int lastIndex = count - 1;
        if (index < lastIndex) {
            mass[index] = mass[lastIndex];
            velocity[index] = velocity[lastIndex];
            position[index] = position[lastIndex];
            cx[index] = cx[lastIndex];
            cy[index] = cy[lastIndex];
        }
        --count;
    }

    public void AddParcel(Vector2 initalPosition, Vector2 initialVelocity) {
        int capacity = mass.Length;
        if (count >= capacity) {
            Array.Resize(ref mass, capacity * 2);
            Array.Resize(ref position, capacity * 2);
            Array.Resize(ref velocity, capacity * 2);
            Array.Resize(ref cx, capacity * 2);
            Array.Resize(ref cy, capacity * 2);
        }

        mass[count] = 1f;
        velocity[count] = initialVelocity;
        position[count] = initalPosition;
        cx[count] = Vector2.zero;
        cy[count] = Vector2.zero;
        ++count;
    }

    /// <summary>
    /// Transfer velocities from the MAC Grid to the Parcels
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    public void TransferVelocities(FluidGridMac grid) {
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

            Vector2 direction = dp.normalized;
            Vector2 cellPosition = new Vector2(
                Mathf.Round(p.x - Mathf.Sign(direction.x) * 1e-3f),
                Mathf.Round(p.y - Mathf.Sign(direction.y) * 1e-3f)
            );
            float horizontalEdgePosition = cellPosition.x + Mathf.Sign(direction.x) * 0.5f;
            float verticalEdgePosition = cellPosition.y + Mathf.Sign(direction.y) * 0.5f;
            float tx = Mathf.Abs(dp.x) > 1e-6f
                ? (horizontalEdgePosition - p.x) / dp.x
                : float.MaxValue;
            float ty = Mathf.Abs(dp.y) > 1e-6f
                ? (verticalEdgePosition - p.y) / dp.y :
                float.MaxValue;

            if (tx <= 1f || ty <= 1f) {
                if (tx < ty) {
                    Vector2Int cellNext = new Vector2Int(
                        Mathf.RoundToInt(cellPosition.x + Mathf.Sign(direction.x)),
                        Mathf.RoundToInt(cellPosition.y)
                    );
                    if (grid.GetCellType(cellNext.x, cellNext.y) == FluidGridMac.CellType.Solid) {
                        // velocity[i].x *= -0.9f;    // Reverse horizontal velocity adding a little bit of dissipation
                        velocity[i].x = 0;
                        dp *= (tx * 0.995f);       // Move the Parcel near the edge
                    }
                }
                else {
                    Vector2Int cellNext = new Vector2Int(
                        Mathf.RoundToInt(cellPosition.x),
                        Mathf.RoundToInt(cellPosition.y + Mathf.Sign(direction.y))
                    );
                    if (grid.GetCellType(cellNext.x, cellNext.y) == FluidGridMac.CellType.Solid) {
                        // velocity[i].y *= -0.9f;    // Reverse vertical velocity adding a little bit of dissipation
                        velocity[i].y = 0;
                        dp *= (ty * 0.995f);       // Move the Parcel near the edge
                    }
                }
            }
            position[i] += dp;
            if (position[i].x <= -1f || position[i].x >= grid.width + 1f ||
                position[i].y <= -1f || position[i].y >= grid.height + 1f) {
                RemoveParcel(i);
                --i; // Avoid skipping a particle
            }
        }
    }
}
}
