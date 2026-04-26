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

    public void TransferGridData(FluidGridMac grid) { }
}
}
