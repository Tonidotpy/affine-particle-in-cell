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
            position[i] = new Vector2(Random.Range(1f, 9f), Random.Range(1f, 9f));
            mass[i] = 1f;
        }
        position[0] = Random.insideUnitCircle + Vector2.one * 2;
        velocity[0] = Random.insideUnitCircle;
    }

    public void TransferGridData(FluidGridMac grid) {
        TransferVelocities(grid);
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
                float wBottomLeft  = (     xFrac) * (     yFrac);
                float wBottomRight = (1f - xFrac) * (     yFrac);
                float wTopLeft     = (     xFrac) * (1f - yFrac);
                float wTopRight    = (1f - xFrac) * (1f - yFrac);

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
                float wBottomLeft  = (     xFrac) * (     yFrac);
                float wBottomRight = (1f - xFrac) * (     yFrac);
                float wTopLeft     = (     xFrac) * (1f - yFrac);
                float wTopRight    = (1f - xFrac) * (1f - yFrac);

                v.y += wBottomLeft * grid.GetVelocity(grid.velocityV, x, yBottom, FluidGridMac.Axis.Y);
                v.y += wBottomRight * grid.GetVelocity(grid.velocityV, x + 1, yBottom, FluidGridMac.Axis.Y);
                v.y += wTopLeft * grid.GetVelocity(grid.velocityV, x, yTop, FluidGridMac.Axis.Y);
                v.y += wTopRight * grid.GetVelocity(grid.velocityV, x + 1, yTop, FluidGridMac.Axis.Y);
            }
            velocity[i] = v;
        }
    }
}
}
