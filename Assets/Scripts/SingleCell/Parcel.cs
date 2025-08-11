using UnityEngine;

public class Parcel {
    private Vector2 _position;
    private Vector2 _velocity;
    private float[,] _affineState;

    public float Mass { get; }
    public Vector2 Position { get { return _position; } }
    public Vector2 Velocity { get { return _velocity; } }

    public Parcel(Vector2 position, Vector2 velocity, float mass = 1.0f) {
        Mass = mass;
        _position = position;
        _velocity = velocity;
        _affineState = new [,] { { 0f, 0f }, { 0f, 0f } };
    }

    public Vector2 CalculateAffineVelocity(Vector2 position) {
        Vector2 diff = position - _position;
        return _velocity + new Vector2(
            _affineState[0,0] * diff.x + _affineState[0,1] * diff.y,
            _affineState[1,0] * diff.x + _affineState[1,1] * diff.y
        );
    }

    public void TransferFromCell(Cell cell) {
        /*
         * 1. Calculate and update velocity
         *  Convert from staggered to node velocities, since there is only a
         *  single cell for each node the velocity vector components are the
         *  nearest staggered velocities. With multiple Cells it is needed to
         *  average the contribution of the nearest Cells velocities
         */
        Vector2 ratio = new Vector2(
            _position.x / cell.Size,
            _position.y / cell.Size
        );
        Vector2[] nodeVelocity = new Vector2[] {
            new Vector2(cell.Velocity[3], cell.Velocity[0]),
            new Vector2(cell.Velocity[1], cell.Velocity[0]),
            new Vector2(cell.Velocity[1], cell.Velocity[2]),
            new Vector2(cell.Velocity[3], cell.Velocity[2])
        };
        _velocity = Vector2.Lerp(
            Vector2.Lerp(nodeVelocity[0], nodeVelocity[1], ratio.x),
            Vector2.Lerp(nodeVelocity[3], nodeVelocity[2], ratio.x),
            ratio.y
        );

        /*
         * 2. Calculate and update affine matrix
         *  The affine matrix consists of the change in x and y velocity over
         *  the x and y directions (Gradient)
         *
         *  The diagonal terms measure the "stretch" and "squish" along the axes
         *  and they are calculated with a direct finite difference
         *
         *  The off-diagonal measure the "shear" of the fluid and they require
         *  an interpolation and then a difference step
         */
        _affineState[0,0] = (cell.Velocity[1] - cell.Velocity[3]) / cell.Size;
        _affineState[1,1] = (cell.Velocity[2] - cell.Velocity[0]) / cell.Size;

        /*
         * Delta used to calculate "virtual points" around the Parcel
         * The value is half of the Cell size to ensure stability and accuracy
         */
        // float delta = cell.Size * 0.5f;
        // Vector2[] virtualPoints = new Vector2[] {
        //     _position + Vector2.down * delta,
        //     _position + Vector2.right * delta,
        //     _position + Vector2.up * delta,
        //     _position + Vector2.left * delta,
        // };
        //
        // float bottomVelocity = virtualPoints[0].y < 0
        //     ? Mathf.Lerp(
        //         0,
        //         Mathf.Lerp(cell.Velocity[3], cell.Velocity[1], ratio.x),
        //         (virtualPoints[0].y + cell.Size) / (cell.Size * 2f)
        //     )
        //     : Mathf.Lerp(
        //         Mathf.Lerp(cell.Velocity[3], cell.Velocity[1], ratio.x),
        //         Mathf.Lerp(cell.Velocity[3], cell.Velocity[1], ratio.x),
        //     )
        //
        // _affineState[0,1] = (topVelocity - bottomVelocity) / cell.Size;
        // _affineState[1,0] = (rightVelocity - leftVelocity) / cell.Size;
    }

    public void Move(float dt, Vector2 velocity) {
        _position = _position + dt * velocity;
    }
}
