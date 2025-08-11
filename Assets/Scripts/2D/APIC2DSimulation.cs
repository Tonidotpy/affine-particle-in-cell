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

    void OnDrawGizmos() {
        if (Application.isPlaying) {
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw Grid bounds
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

            // Draw mass distribution
            for (int x = -1; x <= _grid.Size.x; ++x) {
                for (int y = -1; y <= _grid.Size.y; ++y) {
                    Vector2 pos = new Vector2(
                        x * _grid.CellSize + _grid.CellSize * 0.5f,
                        y * _grid.CellSize + _grid.CellSize * 0.5f
                    );
                    Gizmos.color = Color.Lerp(
                        Color.blue,
                        Color.red,
                        _grid.Mass[(x + 1) * (_grid.Size.y + 2) + (y + 1)]
                    );
                    Gizmos.DrawSphere(pos, 0.1f);
                }
            }
        }
    }

    /// <summary>
    /// Initialize the simulation
    /// </summary>
    void OnEnable() {
        _grid = new StaggeredGrid(
            new int2(4, 4),
            2,
            Allocator.Persistent
        );
        _parcels = new Parcels(
            1,
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
        _grid.TransferMomentum();
        _grid.CalculateVelocity();
    }

    /// <summary>
    /// Main simulation loop
    /// </summary>
    void FixedUpdate() {
        ParcelsToGrid();
    }
}
