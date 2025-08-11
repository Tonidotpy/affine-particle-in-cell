using System;
using System.Linq;
using UnityEngine;

public class Cell {
    private float _mass;

    public float Mass { get { return _mass; } }
    public float Size { get; }
    public float Area { get { return Size * Size; } }
    public float Density { get { return _mass / Area } }
    public float Pressure { get; }
    public float[] Velocity { get; } // Velocity is assumed to be positioned at the center of the edges

    public Cell(float size = 1.0f) {
        Size = size;
        _mass = 1.0f;
        Pressure = 1.0f;
        Velocity = new float[4];
    }

    public void TransferFromParcel(Parcel parcel) {
        /*
         * Here since there is only a single Cell with a single Parcel the
         * calculation for the Cell indices, position and the Parcel local
         * position are not implemented.
         *
         * It is asserted to have the bottom left corner of the Cell on the
         * origin in absolute space and the particle is kept inside it.
         * Based on the previous absumption the local position of the Parcel
         * corresponds to the global position
         */

        /*
         * 1. Mass distribution
         *  Mass distribution is calculated by adding together the Parcel
         *  mass weighted with the ratio between the area covered by the
         *  position of the Parcel (A) and the Cell area:
         *
         *               4 ------------ 3
         *               |              |
         *               |              |
         *               |              |
         *               | - - o        |
         *               | AAAA|        |
         *               1 ------------ 2
         */
        Vector2 ratio = new Vector2(
            parcel.Position.x / Size,
            parcel.Position.y / Size
        ); 
        float[] masses = new float[] {            
            parcel.Mass * (1f - ratio.x) * (1f - ratio.y),
            parcel.Mass * ratio.x * (1f - ratio.y),
            parcel.Mass * ratio.x * ratio.y,
            parcel.Mass * (1f - ratio.x) * ratio.y
        };
        _mass = masses.Sum();

        /*
         * 2. Momentum distribution
         *  Momentum is transfered from the Parcel to the Cell taking into
         *  account the linear momentum and the affine velocity model of the
         *  Parcel
         */
        Vector2[] momentum = new Vector2[4];
        for (int i = 0; i < momentum.Length; ++i) {
            Vector2 position = new Vector2(
                (i & 0b01) ^ ((i & 0b10) >> 1),
                i / 2
            ) * Size;
            Vector2 affineVelocity = parcel.CalculateAffineVelocity(position);
            momentum[i] = masses[i] * affineVelocity;
        }

        /*
         * 3. Velocity distribution
         *  Velocities are distribute dividing the previously calculted
         *  momentum by the Cell mass.
         *  To achieve better simulation stability the velocity is calculated
         *  at the center of the edges by averaging the momentum and masses
         *  at the nodes of the edge
         */
        for (int i = 0; i < Velocity.Length; ++i) { 
            Velocity[i] = (momentum[i][(i + 1) % 2] + momentum[(i + 1) % 4][(i + 1) % 2]) /
                (masses[i] + masses[(i + 1) % 4]);
            Velocity[i] *= 0.5f;
        }
    }

    public void ApplyForces(Vector2 force, float dt) {
        /*
         * Here external forces (such as gravity) are included in the simualtion
         */
        for (int i = 0; i < Velocity.Length; ++i) {
            Velocity[i] += force[(i + 1) % 2] * dt;
        }
    }

    public void EnforceBoundaries() {
        /*
         * Since there is a single Cell the boundary enforcement act on all
         * velocities setting them to 0, in a bigger scenario with more Cells
         * only the outer ones needs to be set
         *
         * This check is not sufficient to enforce the boundaries since the
         * particles can still end out of the boundaries, additional measures
         * must be taken to correct this problem
         */
        for (int i = 0; i < Velocity.Length; ++i) {
            Velocity[i] = 0f;
        } 
    }

    public float CalculateDivergence() {
        return (Velocity[1] - Velocity[3]) + (Velocity[2] - Velocity[4]);
    }

    public void CorrectVelocity(float dt) {
        // Not needed with a single Cell
    }
}
