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
            position[i] = new Vector2((7f / count) * i + 1f, 8f); // Random.insideUnitCircle + Vector2.one * 2;
            velocity[i] = Random.insideUnitCircle;
        }
    }

    public void TransferGridData(FluidGridMac grid) {
        TransferVelocities(grid);
        UpdateAffineState(grid);
    }

    void TransferVelocities(FluidGridMac grid) {
        for (int i = 0; i < count; ++i) {
            Vector2 v = Vector2.zero;
            Vector2 p = position[i];

            // Horizontal axis
            {
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.FloorToInt(p.y);
                float xLeft = x - 0.5f;
                float xRight = x + 0.5f;

                float xFrac = p.x - xLeft;
                float yFrac = p.y - y;
                float wBottomLeft  = (1f - xFrac) * (1f - yFrac);
                float wBottomRight = (     xFrac) * (1f - yFrac);
                float wTopLeft     = (1f - xFrac) * (     yFrac);
                float wTopRight    = (     xFrac) * (     yFrac);

                v.x += wBottomLeft * grid.GetVelocity(grid.velocityU, xLeft, y, FluidGridMac.Axis.X);
                v.x += wBottomRight * grid.GetVelocity(grid.velocityU, xRight, y, FluidGridMac.Axis.X);
                v.x += wTopLeft * grid.GetVelocity(grid.velocityU, xLeft, y + 1, FluidGridMac.Axis.X);
                v.x += wTopRight * grid.GetVelocity(grid.velocityU, xRight, y + 1, FluidGridMac.Axis.X);
            }
            // Vertical axis
            {
                int x = Mathf.FloorToInt(p.x);
                int y = Mathf.RoundToInt(p.y);
                float yBottom = y - 0.5f;
                float yTop = y + 0.5f;

                float xFrac = p.x - x;
                float yFrac = p.y - yBottom;
                float wBottomLeft  = (1f - xFrac) * (1f - yFrac);
                float wBottomRight = (     xFrac) * (1f - yFrac);
                float wTopLeft     = (1f - xFrac) * (     yFrac);
                float wTopRight    = (     xFrac) * (     yFrac);

                v.y += wBottomLeft * grid.GetVelocity(grid.velocityV, x, yBottom, FluidGridMac.Axis.Y);
                v.y += wBottomRight * grid.GetVelocity(grid.velocityV, x + 1, yBottom, FluidGridMac.Axis.Y);
                v.y += wTopLeft * grid.GetVelocity(grid.velocityV, x, yTop, FluidGridMac.Axis.Y);
                v.y += wTopRight * grid.GetVelocity(grid.velocityV, x + 1, yTop, FluidGridMac.Axis.Y);
            }
            velocity[i] = v * 0.25f;
        }
    }

    void UpdateAffineState(FluidGridMac grid) {
        for (int i = 0; i < count; ++i) {
            Vector2 p = position[i];

            // Horizontal axis
            {
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.FloorToInt(p.y);

                float xLeft = x - 0.5f;
                float xRight = x + 0.5f;

                float xFrac = p.x - xLeft;
                float yFrac = p.y - y;
                Vector2 wBottomLeft  = new Vector2(yFrac - 1f, xFrac - 1f);
                Vector2 wBottomRight = new Vector2(1f - yFrac, -xFrac);
                Vector2 wTopLeft     = new Vector2(-yFrac, 1f - xFrac);
                Vector2 wTopRight    = new Vector2(yFrac, xFrac);

                Vector2 c = Vector2.zero;
                c += wBottomLeft * grid.GetVelocity(grid.velocityU, xLeft, y, FluidGridMac.Axis.X);
                c += wBottomRight * grid.GetVelocity(grid.velocityU, xRight, y, FluidGridMac.Axis.X);
                c += wTopLeft * grid.GetVelocity(grid.velocityU, xLeft, y + 1, FluidGridMac.Axis.X);
                c += wTopRight * grid.GetVelocity(grid.velocityU, xRight, y + 1, FluidGridMac.Axis.X);
                cx[i] = c;
            }
            // Vertical axis
            {
                int x = Mathf.FloorToInt(p.x);
                int y = Mathf.RoundToInt(p.y);

                float yBottom = y - 0.5f;
                float yTop = y + 0.5f;

                float xFrac = p.x - x;
                float yFrac = p.y - yBottom;
                Vector2 wBottomLeft  = new Vector2(yFrac - 1f, xFrac - 1f);
                Vector2 wBottomRight = new Vector2(1f - yFrac, -xFrac);
                Vector2 wTopLeft     = new Vector2(-yFrac, 1f - xFrac);
                Vector2 wTopRight    = new Vector2(yFrac, xFrac);

                Vector2 c = Vector2.zero;
                c += wBottomLeft * grid.GetVelocity(grid.velocityV, x, yBottom, FluidGridMac.Axis.Y);
                c += wBottomRight * grid.GetVelocity(grid.velocityV, x + 1, yBottom, FluidGridMac.Axis.Y);
                c += wTopLeft * grid.GetVelocity(grid.velocityV, x, yTop, FluidGridMac.Axis.Y);
                c += wTopRight * grid.GetVelocity(grid.velocityV, x + 1, yTop, FluidGridMac.Axis.Y);
                cy[i] = c;
            }
        }
    }

    public void Advect(FluidGridMac grid, float dt) {
        for (int i = 0; i < count; ++i) {
            Vector2 p = position[i];

            Vector2 k1 = velocity[i];
            Vector2 k2 = grid.SampleVelocity(p + k1 * (dt * 0.5f));
            Vector2 k3 = grid.SampleVelocity(p + k2 * (dt * 0.5f));
            Vector2 k4 = grid.SampleVelocity(p + k3 * dt);

            // RK4
            Vector2 pNext = p + (dt / 6f) * (k1 + 2 * k2 + 2 * k3 + k4);
            position[i] = new Vector2(
                Mathf.Clamp(pNext.x, 0.5f, grid.width - 1.5f),
                Mathf.Clamp(pNext.y, 0.5f, grid.height - 1.5f)
            );
        }
    }
}
}
