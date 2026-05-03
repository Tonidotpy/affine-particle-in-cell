using UnityEngine;

namespace FluidSimulationCPU {
/// <summary>
/// Representation of clusters of particles sharing the same state and behaviors.
/// Each parcel may contain multiple particles having the same position and velocity
/// </summary>
public class FluidParcels {
    public readonly int count;

    public readonly float[] mass;
    public readonly Vector2[] position;
    public readonly Vector2[] velocity;

    // Affine State vectors
    public readonly Vector2[] cx;
    public readonly Vector2[] cy;

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

    /// <summary>
    /// Transfer velocities from the MAC Grid to the Parcels and update the
    /// Affine State vectors using the new Grid velocities
    /// </summary>
    /// <param name="grid">The MAC Grid to get the data from</param>
    public void TransferGridData(FluidGridMac grid) {
        TransferVelocities(grid);
        UpdateAffineState(grid);
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
                    float velocity = grid.GetVelocity(grid.velocityU, x[j], y[j], FluidGridMac.Axis.X);
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
                    float velocity = grid.GetVelocity(grid.velocityV, x[j], y[j], FluidGridMac.Axis.Y);
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
    void UpdateAffineState(FluidGridMac grid) {
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
                    float velocity = grid.GetVelocity(grid.velocityU, x[j], y[j], FluidGridMac.Axis.X);
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
                    float velocity = grid.GetVelocity(grid.velocityV, x[j], y[j], FluidGridMac.Axis.Y);
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

            // TODO: Consider Grid solid cells
            // RK4 implementation
            Vector2 pNext = p + (dt / 6f) * (k1 + 2 * k2 + 2 * k3 + k4);
            position[i] = new Vector2(
                Mathf.Clamp(pNext.x, -0.5f, grid.width - 0.5f),
                Mathf.Clamp(pNext.y, -0.5f, grid.height - 0.5f)
            );
        }
    }
}
}
