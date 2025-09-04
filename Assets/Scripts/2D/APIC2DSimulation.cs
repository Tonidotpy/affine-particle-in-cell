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
    private int _allocationCount = 0;
    private int _disposeCount = 0;

    private StaggeredGrid _grid;
    private Parcels _parcels;
    private GaussSeidelPressureSolver _pressureSolver;

    [Header("Debug Grid")]
    public bool DrawGrid = true;
    public bool DrawGridCellType = false;
    public bool DrawGridMass = false;
    public bool DrawGridMomentum = false;
    public bool DrawGridVelocity = false;
    public bool DrawGridDivergence = false;
    public bool DrawGridPressure = false;
    [Header("Debug Parcels")]
    public bool DrawParcels = true;
    public bool DrawParcelsVelocity = false;

    /// <summary>
    /// Draw the Grid on the Editor for debug
    /// </summary>
    private void DrawGridGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;

        /* Draw X lines */
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f);
        for (int x = 0; x <= _grid.BoundedSize.x; ++x) {
            Gizmos.DrawLine(
                new Vector2(x, -1) * _grid.CellSize,
                new Vector2(x, _grid.BoundedSize.y + 1) * _grid.CellSize
            );
        }
        /* Draw Y lines */
        for (int y = 0; y <= _grid.BoundedSize.y; ++y) {
            Gizmos.DrawLine(
                new Vector2(-1, y) * _grid.CellSize,
                new Vector2(_grid.BoundedSize.x + 1, y) * _grid.CellSize
            );
        }

        /* Draw extended Grid bounds */
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f);
        Gizmos.DrawWireCube(
            new Vector2(
                _grid.BoundedSize.x,
                _grid.BoundedSize.y
            ) * (_grid.CellSize * 0.5f),
            new Vector2(
                _grid.Size.x,
                _grid.Size.y
            ) * _grid.CellSize
        );

        /* Draw simulation Grid bounds */
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
    }

    /// <summary>
    /// Draw Grid C in the Editor for debug
    /// </summary>
    private void DrawGridCellTypeGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 center = new Vector2(x - 0.5f, y - 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x + 1, _grid.Size.y + 1, y + 1);
                const float alpha = 0.3f;
                switch (_grid.Type[index]) {
                    case CellType.Fluid:
                        Gizmos.color = new Color(0.1f, 0.3f, 1f, alpha);
                        break;
                    case CellType.Solid:
                        Gizmos.color = new Color(1f, 0.4f, 0.1f);
                        break;
                    default:
                        Gizmos.color = new Color(0f, 0f, 0f, 0f);
                        break;
                }
                Gizmos.DrawCube(center, Vector2.one * _grid.CellSize);
            }
        }
    }

    /// <summary>
    /// Draw the Grid mass disrtibution on the Editor for debug
    /// </summary>
    private void DrawGridMassGizmos() {
        /*
         * Mass normalization used for color interpolation
         */
        System.Func<float, float> normalize = (float mass) => {
            float ratio = _grid.Area / _parcels.Count;
            ratio = 2 * ratio + 2;
            float t = mass * ratio;
            return (t * 0.35f) / (t * 0.35f + 1f);
        };
        float gizmosSize = _grid.CellSize * 0.08f;

        Gizmos.matrix = transform.localToWorldMatrix;

        /*
         * Draw Cell center mass
         */
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 center = new Vector2(x - 0.5f, y - 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y, y);
                float t = normalize(_grid.Mass[index]);
                Gizmos.color = Color.Lerp(
                    Color.blue,
                    Color.red,
                    t
                );
                Gizmos.DrawCube(center, Vector2.one * gizmosSize);
            }
        }

        /*
         * Draw Cell X staggered mass
         */
        for (int x = 0; x < _grid.Size.x + 1; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 center = new Vector2(x - 1f, y - 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y, y);
                float t = normalize(_grid.MassX[index]);
                Gizmos.color = Color.Lerp(
                    Color.blue,
                    Color.red,
                    t
                );
                Gizmos.DrawCube(center, Vector2.one * gizmosSize);
            }
        }

        /*
         * Draw Cell Y staggered mass
         */
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y + 1; ++y) {
                Vector2 center = new Vector2(x - 0.5f, y - 1f);
                center *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y + 1, y);
                float t = normalize(_grid.MassY[index]);
                Gizmos.color = Color.Lerp(
                    Color.blue,
                    Color.red,
                    t
                );
                Gizmos.DrawCube(center, Vector2.one * gizmosSize);
            }
        }
    }

    /// <summary>
    /// Draw the Grid momentum on the Editor for debug
    /// </summary>
    private void DrawGridMomentumGizmos() {
        System.Func<float, float> normalize = (float momentum) => {
            float t = math.abs(momentum);
            float norm = t / (t + 1f);
            if (momentum < 0)
                norm *= -1;
            return norm;
        };
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;

        /*
         * Draw staggered X momentum
         */
        for (int x = 0; x < _grid.Size.x + 1; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 origin = new Vector2(x - 1f, y - 0.5f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y, y);
                float length = normalize(_grid.MomentumX[index]) * 0.5f;
                Vector2 dir = Vector2.right * length;
                Gizmos.DrawLine(origin, origin + dir);
            }
        }

        /*
         * Draw staggered Y momentum
         */
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y + 1; ++y) {
                Vector2 origin = new Vector2(x - 0.5f, y - 1f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y + 1, y);
                float length = normalize(_grid.MomentumY[index]) * 0.5f;
                Vector2 dir = Vector2.up * length;
                Gizmos.DrawLine(origin, origin + dir);
            }
        }
    }

    /// <summary>
    /// Draw Grid staggered velocities in the Editor for debug
    /// </summary>
    private void DrawGridVelocityGizmos() {
        System.Func<float, float> normalize = (float v) => {
            float t = math.abs(v);
            float norm = t / (t + 1f);
            if (v < 0)
                norm *= -1;
            return norm;
        };
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;

        /*
         * Draw staggered X velocity
         */
        for (int x = 0; x < _grid.Size.x + 1; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 origin = new Vector2(x - 1f, y - 0.5f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y, y);
                float length = normalize(_grid.VelocityX[index]) * 0.5f;
                Vector2 dir = Vector2.right * length;
                Gizmos.DrawLine(origin, origin + dir);
            }
        }

        /*
         * Draw staggered Y velocity
         */
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y + 1; ++y) {
                Vector2 origin = new Vector2(x - 0.5f, y - 1f);
                origin *= _grid.CellSize;

                int index = math.mad(x, _grid.Size.y + 1, y);
                float length = normalize(_grid.VelocityY[index]) * 0.5f;
                Vector2 dir = Vector2.up * length;
                Gizmos.DrawLine(origin, origin + dir);
            }
        }
    }

    /// <summary>
    /// Draw Grid divergence in the Editor for debug
    /// </summary>
    private void DrawGridDivergenceGizmos() {
        /*
         * Divergence normalization used for color interpolation
         */
        System.Func<float, float> normalize = (float divergence) => {
            float t = math.abs(divergence);
            return t / (t + 1f);
        };

        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x < _grid.BoundedSize.x; ++x) {
            for (int y = 0; y < _grid.BoundedSize.y; ++y) {
                Vector2 center = new Vector2(x + 0.5f, y + 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x, _grid.BoundedSize.y, y);
                float divergence = _grid.Divergence[index];
                float t = normalize(divergence);
                const float alpha = 0.4f;
                if (divergence < 0) {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, 0),
                        new Color(0, 1f, 0, alpha),
                        t
                    );
                }
                else {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, 0),
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
    private void DrawGridPressureGizmos() {
        /*
         * Pressure normalization used for color interpolation
         */
        System.Func<float, float> normalize = (float pressure) => {
            float t = math.abs(pressure);
            return t / (t + 1f);
        };

        Gizmos.matrix = transform.localToWorldMatrix;
        for (int x = 0; x < _grid.Size.x; ++x) {
            for (int y = 0; y < _grid.Size.y; ++y) {
                Vector2 center = new Vector2(x - 0.5f, y - 0.5f);
                center *= _grid.CellSize;

                int index = math.mad(x + 1, _grid.Size.y + 1, y + 1);
                float pressure = _grid.Pressure[index];
                float t = normalize(pressure);
                const float alpha = 0.4f;
                if (pressure < 0) {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, 0),
                        new Color(0, 1f, 0, alpha),
                        t
                    );
                }
                else {
                    Gizmos.color = Color.Lerp(
                        new Color(0, 0, 1f, 0),
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
            if (DrawGridCellType) DrawGridCellTypeGizmos();
            if (DrawGridMass) DrawGridMassGizmos();
            if (DrawGridMomentum) DrawGridMomentumGizmos();
            if (DrawGridVelocity) DrawGridVelocityGizmos();
            if (DrawGridDivergence) DrawGridDivergenceGizmos();
            if (DrawGridPressure) DrawGridPressureGizmos();

            if (DrawParcels) DrawParcelsGizmos();
            if (DrawParcelsVelocity) DrawParcelsVelocityGizmos();
        }
    }

    /// <summary>
    /// Initialize the simulation
    /// </summary>
    void Start() {
        ++_allocationCount;
        Debug.Log("Allocating memory: " + _allocationCount);
        _grid = new StaggeredGrid(
            new int2(7, 7),
            2,
            1000f,
            1.225f,
            Allocator.Persistent
        );
        _parcels = new Parcels(
            3,
            0.1f,
            Allocator.Persistent
        );
        _pressureSolver = new GaussSeidelPressureSolver(20);
    }

    /// <summary>
    /// Dispose simulation resources
    /// </summary>
    void OnApplicationQuit() {
        ++_disposeCount;
        Debug.Log("Disposing memory: " + _disposeCount);
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
        // _grid.EnforceBoundaries();
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
        // _parcels.UpdateAffineState(_grid);
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
        AdvectParcels();
        ParcelsToGrid();
        UpdateGrid(Time.fixedDeltaTime);
        ProjectPressure(Time.fixedDeltaTime);
        GridToParcels();
    }
}
