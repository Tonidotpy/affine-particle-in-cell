using UnityEngine;

public class Simulation : MonoBehaviour {
    private Parcel _parcel = new Parcel(
        new Vector2(5f, 9f),
        new Vector2(0.0f, -0.1f).normalized
    );
    private Cell _cell = new Cell(10f);

    private void Particle2Grid() {
        _cell.TransferFromParcel(_parcel);
    }

    private void UpdateGrid(float dt) {
        _cell.ApplyForces(Physics.gravity, dt);
        _cell.EnforceBoundaries();
    }

    private void ProjectPressure(float dt) {
        /*
         * Pressure projection is not needed with a single Cell since there is
         * no concept of pressure gradients between Cells
         */
        float divergence = _cell.CalculateDivergence();
        // TODO: Run pressure solver
        _cell.CorrectVelocity(dt);
    }

    private void Grid2Particle() { }

    private void AdvectParcel(float dt) {
        // TODO: give as argument the velocity of the next timestep
        _parcel.Move(dt, _parcel.Velocity);
    }


    private void DrawCellMassDistribution() {
        float pointSize = _cell.Size * 0.02f;
        // Draw Parcel mass representation
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_parcel.Position, pointSize);

        // Draw Cell mass representation
        Vector2 ratio = new Vector2(
            _parcel.Position.x / _cell.Size,
            _parcel.Position.y / _cell.Size
        );

        // Bottom-Left
        Gizmos.color = Color.Lerp(Color.blue, Color.red, _parcel.Mass * (1f - ratio.x) * (1f - ratio.y));
        Gizmos.DrawSphere(Vector2.zero, pointSize);
        // Bottom-Right
        Gizmos.color = Color.Lerp(Color.blue, Color.red, _parcel.Mass * ratio.x * (1f - ratio.y));
        Gizmos.DrawSphere(Vector2.right * _cell.Size, pointSize);
        // Top-Right
        Gizmos.color = Color.Lerp(Color.blue, Color.red, _parcel.Mass * ratio.x * ratio.y);
        Gizmos.DrawSphere(Vector2.one * _cell.Size, pointSize);
        // Top-Left
        Gizmos.color = Color.Lerp(Color.blue, Color.red, _parcel.Mass * (1f - ratio.x) * ratio.y);
        Gizmos.DrawSphere(Vector2.up * _cell.Size, pointSize);
    }

    private void DrawCellVelocityDistribution() {
        // Bottom
        Vector2 bottom = Vector2.right * _cell.Size * 0.5f;
        float bottomIntensity = Mathf.Atan(_cell.Velocity[0]) / Mathf.PI;
        Gizmos.color = Color.Lerp(Color.blue, Color.red, bottomIntensity);
        Gizmos.DrawLine(bottom, bottom + Vector2.up * bottomIntensity);

        // Right
        Vector2 right = new Vector2 (_cell.Size, _cell.Size * 0.5f);
        float rightIntensity = Mathf.Atan(_cell.Velocity[1]) / Mathf.PI;
        Gizmos.color = Color.Lerp(Color.blue, Color.red, rightIntensity);
        Gizmos.DrawLine(right, right + Vector2.left * rightIntensity);

        // Top
        Vector2 top = new Vector2 (_cell.Size * 0.5f, _cell.Size);
        float topIntensity = Mathf.Atan(_cell.Velocity[2]) / Mathf.PI;
        Gizmos.color = Color.Lerp(Color.blue, Color.red, topIntensity);
        Gizmos.DrawLine(top, top + Vector2.down * topIntensity);

        // Left
        Vector2 left = Vector2.up * _cell.Size * 0.5f;
        float leftIntensity = Mathf.Atan(_cell.Velocity[3]) / Mathf.PI;
        Gizmos.color = Color.Lerp(Color.blue, Color.red, leftIntensity);
        Gizmos.DrawLine(left, left + Vector2.right * leftIntensity);
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.white;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.one * _cell.Size * 0.5f, Vector2.one * _cell.Size);

        DrawCellMassDistribution();
        DrawCellVelocityDistribution();
    }

    void FixedUpdate() {
        // float t = Mathf.Sin(Time.fixedTime) * 0.5f + 0.5f;
        // _parcel.Position = new Vector2(0, t);
        // float v = Mathf.Cos(Time.fixedTime);
        // _parcel.Velocity = new Vector2(0, v);

        Particle2Grid();
        UpdateGrid(Time.fixedDeltaTime);
        // ProjectPressure(Time.fixedDeltaTime);
    }
}
