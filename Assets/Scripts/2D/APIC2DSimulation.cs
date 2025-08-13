using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Affine Particle-In-Cell (APIC) 2D fluid simulation
/// </summary>
/// <para>
/// Attach to an empty object to simulate a fluid in two dimensions
/// </para>
public class APIC2DSimulation : MonoBehaviour {
    public StaggeredGrid _grid;
    public Parcels _parcels;

    public bool DrawGrid = true;
    public bool DrawParcels = true;
    public bool DrawMassDistribution = false;
    public bool DrawNodeMassDistribution = false;
    public bool DrawNodeMomentum = false;
    public bool DrawVelocity = false;

    /// <summary>
    /// Draw the Grid on the Editor for debug
    /// </summary>
    private void DrawGridGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(
            new Vector2(
                _grid.Size.x * _grid.CellSize * 0.5f,
                _grid.Size.y * _grid.CellSize * 0.5f
            ),
            new Vector2(
                _grid.Size.x * _grid.CellSize,
                _grid.Size.y * _grid.CellSize
            )
        );

        // Draw x lines
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f);
        for (int x = 1; x < _grid.Size.x; ++x) {
            Gizmos.DrawLine(
                new Vector2(x, 0) * _grid.CellSize,
                new Vector2(x, _grid.Size.y) * _grid.CellSize
            );
        }
        // Draw y lines
        for (int y = 1; y < _grid.Size.y; ++y) {
            Gizmos.DrawLine(
                new Vector2(0, y) * _grid.CellSize,
                new Vector2(_grid.Size.x, y) * _grid.CellSize
            );
        }
    }

    /// <summary>
    /// Draw the Parcels on the Editor for debug
    /// </summary>
    private void DrawParcelsGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.white;
        foreach (float2 pos in _parcels.Position) {
            Gizmos.DrawSphere(
                new Vector2(pos.x, pos.y),
                0.05f
            );
        }
    }

    /// <summary>
    /// Draw the Grid mass disrtibution on the Editor for debug
    /// </summary>
    private void DrawMassDistributionGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = -1; x <= _grid.Size.x; ++x) {
            for (int y = -1; y <= _grid.Size.y; ++y) {
                Vector2 position = new Vector2(
                    math.mad(x, _grid.CellSize, _grid.CellSize * 0.5f),
                    math.mad(y, _grid.CellSize, _grid.CellSize * 0.5f)
                );
                // TODO: Normalize mass for color interpolation
                Gizmos.color = Color.Lerp(
                    Color.blue,
                    Color.red,
                    _grid.Mass[math.mad(
                        x + 1,
                        _grid.Size.y + 2,
                        y + 1
                    )]
                );
                Gizmos.DrawSphere(position, 0.1f);

                if (DrawNodeMassDistribution && x >= 0 && y >= 0) {
                    Vector2 nodePosition = new Vector2(
                        x * _grid.CellSize,
                        y * _grid.CellSize
                    );
                    // TODO: Normalize mass for color interpolation
                    Gizmos.color = Color.Lerp(
                        Color.blue,
                        Color.red,
                        _grid.NodeMass[math.mad(
                            x,
                            _grid.Size.y + 1,
                            y
                        )]
                    );
                    Gizmos.DrawCube(nodePosition, Vector2.one * 0.15f);
                }
            }
        }
    }

    /// <summary>
    /// Draw the Grid momentum on the Editor for debug
    /// </summary>
    private void DrawNodeMomentumGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x <= _grid.Size.x; ++x) {
            for (int y = 0; y <= _grid.Size.y; ++y) {
                Vector2 pos = new Vector2(
                    x * _grid.CellSize,
                    y * _grid.CellSize 
                );
                int index = math.mad(x, _grid.Size.y + 1, y);
                Vector2 dir = new Vector2(
                    _grid.NodeMomentum[index].x,
                    _grid.NodeMomentum[index].y
                );
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(pos, pos + dir);
            }
        }
    }

    /// <summary>
    /// Draw Grid staggered velocities in the Editor for debug
    /// </summary>
    private void DrawVelocityGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        // Draw X staggered velocities
        for (int x = 0; x < _grid.Size.x + 1; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 origin = new Vector2(
                    x * _grid.CellSize,
                    (y + 0.5f) * _grid.CellSize
                );
                int index = math.mad(x, _grid.Size.y, y);
                Gizmos.DrawLine(
                    origin,
                    origin + Vector2.right * _grid.VelocityX[index]
                );
            }
        }

        // Draw X staggered velocities
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y + 1; ++y) {
                Vector2 origin = new Vector2(
                    (x + 0.5f) * _grid.CellSize,
                    y * _grid.CellSize
                );
                int index = math.mad(x, _grid.Size.y + 1, y);
                Gizmos.DrawLine(
                    origin,
                    origin + Vector2.up * _grid.VelocityY[index]
                );
            }
        }
    }

    /// <summary>
    /// Draw visual debug information
    /// </summary>
    void OnDrawGizmos() {
        if (Application.isPlaying) {
            if (DrawGrid) DrawGridGizmos();
            if (DrawParcels) DrawParcelsGizmos();
            if (DrawMassDistribution) DrawMassDistributionGizmos();
            if (DrawNodeMomentum) DrawNodeMomentumGizmos();
            if (DrawVelocity) DrawVelocityGizmos();
        }
    }

    /// <summary>
    /// Initialize the simulation
    /// </summary>
    void OnEnable() {
        _grid = new StaggeredGrid(
            new int2(4, 3),
            2,
            Allocator.Persistent
        );
        _parcels = new Parcels(
            10,
            Allocator.Persistent
        );
    }

    /// <summary>
    /// Dispose simulation resources
    /// </summary>
    void OnDisable() {
        _grid.Dispose();
        _parcels.Dispose();
    }

    /// <summary>
    /// 1. Transfer Parcels information to the Grid
    /// </summary>
    private void ParcelsToGrid() {
        _grid.Reset();
        _grid.TransferMass(_parcels);
        _grid.TransferMomentum(_parcels);
        _grid.CalculateVelocity();
    }

    /// <summary>
    /// 2. Update Grid information applying external forces an enforcing boundaries
    /// </summary>
    /// <param name="dt">The time step of the simulation</param>
    private void UpdateGrid(float dt) {
        float2 g = new float2(0, -9.81f);
        _grid.ApplyExternalForces(g, dt);
        _grid.EnforceBoundaries();
    }

    /// <summary>
    /// 3. Project pressure to solve the Poisson equation and achieve zero divergence,
    /// hence fluid volume conservation
    /// </summary>
    /// <param name="dt">The time step of the simulation</param>
    private void ProjectPressure() { }

    /// <summary>
    /// Main simulation loop
    /// </summary>
    void FixedUpdate() {
        ParcelsToGrid();
        UpdateGrid(Time.fixedDeltaTime);
        ProjectPressure();
    }
}
