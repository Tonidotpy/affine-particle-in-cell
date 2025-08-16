using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Affine Particle-In-Cell (APIC) 2D fluid simulation
/// </summary>
/// <para>
/// Attach to an empty object to simulate a fluid in two dimensions
/// </para>
/// <para>
/// The APIC simulation Loop works with the following steps:
/// 1. Parcels to Grid (P2G)
///     - Transfer Parcels mass and momentum to the Grid. The affine matrix is
///       used here to get a more accurate Grid velocity field.
///     - Calculate initial Grid velocities from the transferred mass and
///       momentum
/// 2. Grid Update
///     - Apply external forces (like gravity) to the Grid velocities.
///     - Enforce boundary conditions by modifying velocities at solid walls.
/// 3. Pressure Projection
///     - Calculate the divergence of the Grid velocity field.
///     - Solve the pressure Poisson equation to find pressures.
///     - Make the velocity field incompressible by substracting the pressure
///       gradient from the Grid velocities.
/// 4. Grid to Parcels (G2P)
///     - Update each parcel's linear velocity by interpolating from the
///       corrected Grid.
///     - Update each parcel's affine matrix by calculating the velocity
///       gradient from the corrected Grid.
/// 5. Parcels Advection
///     - Move each Parcel according to its new velocity over the time step.
/// </para>
public class APIC2DSimulation : MonoBehaviour {
    private StaggeredGrid _grid;
    private Parcels _parcels;
    private GaussSeidelPressureSolver _pressureSolver;

    public bool DrawGrid = true;
    public bool DrawMassDistribution = false;
    public bool DrawNodeMassDistribution = false;
    public bool DrawNodeMomentum = false;
    public bool DrawVelocity = false;
    public bool DrawDivergence = false;
    public bool DrawPressure = false;
    public bool DrawParcels = true;
    public bool DrawParcelsVelocity = false;

    /// <summary>
    /// Draw the Grid on the Editor for debug
    /// </summary>
    private void DrawGridGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(
            new Vector2(
                _grid.BoundedSize.x,
                _grid.BoundedSize.y
            ) * (_grid.CellSize * 0.5f),
            new Vector2(
                _grid.BoundedSize.x,
                _grid.BoundedSize.y
            ) * _grid.CellSize
        );

        // Draw x lines
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f);
        for (int x = 1; x < _grid.BoundedSize.x; ++x) {
            Gizmos.DrawLine(
                new Vector2(x, 0) * _grid.CellSize,
                new Vector2(x, _grid.BoundedSize.y) * _grid.CellSize
            );
        }
        // Draw y lines
        for (int y = 1; y < _grid.BoundedSize.y; ++y) {
            Gizmos.DrawLine(
                new Vector2(0, y) * _grid.CellSize,
                new Vector2(_grid.BoundedSize.x, y) * _grid.CellSize
            );
        }
    }

    /// <summary>
    /// Draw the Grid mass disrtibution on the Editor for debug
    /// </summary>
    private void DrawMassDistributionGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;

        // Draw center masses
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 position = new Vector2(x - 0.5f, y - 0.5f);
                position *= _grid.CellSize;

                // Normalize mass for color interpolation
                float ratio = _grid.Area / _parcels.Count;
                ratio = 2 * ratio + 2;
                int index = math.mad(x, _grid.Size.y, y);
                float t = _grid.Mass[index];
                t = (t * ratio * 0.35f) / (t * ratio * 0.35f + 1f);
                Gizmos.color = Color.Lerp(
                    Color.blue,
                    Color.red,
                    t
                );
                Gizmos.DrawSphere(position, 0.1f);
            }
        }

        // Draw node masses
        if (DrawNodeMassDistribution) {
            for (int x = 0; x < _grid.Size.x + 1; ++x) {
                for (int y = 0; y < _grid.Size.y + 1; ++y) {
                    Vector2 nodePosition = new Vector2(x - 1f, y - 1f);
                    nodePosition *= _grid.CellSize;

                    // Normalize mass for color interpolation
                    float ratio = (_grid.Size.x + 1) * (_grid.Size.y + 1) / _parcels.Count;
                    ratio = 2 * ratio + 2;
                    int index = math.mad(x, _grid.Size.y + 1, y);
                    float t = _grid.NodeMass[index];
                    t = (t * ratio * 0.35f) / (t * ratio * 0.35f + 1f);
                    Gizmos.color = Color.Lerp(
                        Color.blue,
                        Color.red,
                        t
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
        for (int x = 0; x < _grid.Size.x + 1; ++x) {
            for (int y = 0; y < _grid.Size.y + 1; ++y) {
                Vector2 origin = new Vector2(x - 1f, y - 1f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y + 1, y);
                Vector2 dir = new Vector2(
                    _grid.NodeMomentum[index].x,
                    _grid.NodeMomentum[index].y
                );
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, origin + dir);
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
        for (int x = 1; x < _grid.Size.x - 1; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 origin = new Vector2(x - 1f, y - 0.5f);
                origin *= _grid.CellSize;

                int index = math.mad(x - 1, _grid.Size.y, y);
                float magnitude = math.abs(_grid.VelocityX[index]);
                magnitude = magnitude / (magnitude + 1f);
                if (_grid.VelocityX[index] < 0)
                    magnitude *= -1f;
                Gizmos.DrawLine(
                    origin,
                    origin + Vector2.right * (magnitude * 0.5f)
                );
            }
        }

        // Draw Y staggered velocities
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 1; y < _grid.Size.y; ++y) {
                Vector2 origin = new Vector2(x - 0.5f, y - 1f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.BoundedSize.y + 1, y - 1);
                float magnitude = Mathf.Abs(_grid.VelocityY[index]);
                magnitude = magnitude / (magnitude + 1f);
                if (_grid.VelocityY[index] < 0)
                    magnitude *= -1f;
                Gizmos.DrawLine(
                    origin,
                    origin + Vector2.up * (magnitude * 0.5f)
                );
            }
        }
    }

    /// <summary>
    /// Draw Grid divergence in the Editor for debug
    /// </summary>
    private void DrawDivergenceGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x < _grid.BoundedSize.x; ++x) {
            for (int y = 0; y < _grid.BoundedSize.y; ++y) {
                Vector2 center = new Vector2(x + 0.5f, y + 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x, _grid.BoundedSize.y, y);
                float divergence = _grid.Divergence[index];
                float absDivergence = math.abs(divergence);
                float t = absDivergence / (absDivergence + 1);
                const float alpha = 0.2f;
                if (divergence < 0) {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, alpha),
                        new Color(0, 1f, 0, alpha),
                        t
                    );
                }
                else {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, alpha),
                        new Color(1f, 0, 0, alpha),
                        t
                    );
                }
                Gizmos.DrawCube(center, Vector2.one * _grid.CellSize);
                Handles.Label(transform.position + new Vector3(center.x - 0.4f, center.y + 0.4f, 0), divergence.ToString());
            }
        }
    }

    /// <summary>
    /// Draw Grid pressure in the Editor for debug
    /// </summary>
    private void DrawPressureGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 center = new Vector2(x - 0.5f, y - 0.5f);
                center *= _grid.CellSize;

                // Normalize mass for color interpolation
                int index = math.mad(x, _grid.Size.y, y);
                float pressure = _grid.Pressure[index];
                float absPressure = math.abs(pressure);
                float t = absPressure / (absPressure + 1f);
                const float alpha = 0.2f;
                if (pressure < 0) {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, alpha),
                        new Color(0, 1f, 0, alpha),
                        t
                    );
                }
                else {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, alpha),
                        new Color(1f, 0, 0, alpha),
                        t
                    );
                }
                Gizmos.DrawCube(center, Vector2.one * _grid.CellSize);
                Handles.Label(transform.position + new Vector3(center.x - 0.4f, center.y + 0.4f, 0), pressure.ToString());
            }
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
    /// Draw the Parcels velocity on the Editor for debug
    /// </summary>
    private void DrawParcelsVelocityGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < _parcels.Count; ++i) {
            Vector2 origin = new Vector2(_parcels.Position[i].x, _parcels.Position[i].y);
            Vector2 dir = new Vector2(_parcels.Velocity[i].x, _parcels.Velocity[i].y);
            Gizmos.DrawLine(origin, origin + dir);
        }
    }



    /// <summary>
    /// Draw visual debug information
    /// </summary>
    void OnDrawGizmos() {
        if (Application.isPlaying) {
            if (DrawGrid) DrawGridGizmos();
            if (DrawMassDistribution) DrawMassDistributionGizmos();
            if (DrawNodeMomentum) DrawNodeMomentumGizmos();
            if (DrawVelocity) DrawVelocityGizmos();
            if (DrawDivergence) DrawDivergenceGizmos();
            if (DrawPressure) DrawPressureGizmos();

            if (DrawParcels) DrawParcelsGizmos();
            if (DrawParcelsVelocity) DrawParcelsVelocityGizmos();
        }
    }

    /// <summary>
    /// Initialize the simulation
    /// </summary>
    void OnEnable() {
        _grid = new StaggeredGrid(
            new int2(4, 3),
            2,
            1,
            Allocator.Persistent
        );
        _parcels = new Parcels(
            10,
            0.1f,
            Allocator.Persistent
        );
        _pressureSolver = new GaussSeidelPressureSolver(20);
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
    private void ProjectPressure(float dt) {
        _grid.CalculateDivergence();
        _pressureSolver.Solve(_grid, dt);
        _grid.CorrectVelocity(dt);
    }

    /// <summary>
    /// 4. Transfer Grid information to the Parcels
    /// </summary>
    private void GridToParcels() {
        _parcels.UpdateVelocity(_grid);
        _parcels.UpdateAffineState(_grid);
    } 

    /// <summary>
    /// 5. Update Parcels position based on their velocities (Advection)
    /// </summary>
    private void AdvectParcels() {
        _parcels.Move(_grid, Time.fixedDeltaTime);
    } 

    /// <summary>
    /// Main simulation loop
    /// </summary>
    void FixedUpdate() {
        ParcelsToGrid();
        UpdateGrid(Time.fixedDeltaTime);
        ProjectPressure(Time.fixedDeltaTime);
        GridToParcels();
        AdvectParcels();
    }
}
