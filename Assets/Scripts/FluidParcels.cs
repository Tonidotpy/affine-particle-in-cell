using UnityEngine;

namespace FluidSimulation {
    public class FluidParcels {
        public FluidGrid grid;

        public readonly int count;
        public readonly float[] mass;
        public readonly Vector2[] position;
        public readonly Vector2[] velocity;

        public readonly Vector2[] cX;
        public readonly Vector2[] cY;

        public FluidParcels(int count) {
            this.count = count;
            mass = new float[count];
            position = new Vector2[count];
            velocity = new Vector2[count];
            cX = new Vector2[count];
            cY = new Vector2[count];

            // TODO: Remove
            position[0] = new Vector2(0.8f, 0.4f);
            // position[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < count; ++i) {
                mass[i] = 1f;
            }
        }

        public void PairGrid(FluidGrid grid) {
            this.grid = grid;
        }
    }
}
