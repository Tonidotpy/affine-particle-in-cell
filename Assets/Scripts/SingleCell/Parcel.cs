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
        // Calculate and update velocity
        _velocity = new Vector2(
            (cell.Velocity[1] + cell.Velocity[3]) * 0.5f,
            (cell.Velocity[0] + cell.Velocity[2]) * 0.5f
        );

        // TODO: Calculate and update affine matrix
    }

    public void Move(float dt, Vector2 velocity) {
        _position = _position + dt * velocity;
    }
}
