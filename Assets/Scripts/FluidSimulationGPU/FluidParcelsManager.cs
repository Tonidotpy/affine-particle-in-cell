using UnityEngine;
using System;
using Seb.Helpers;

namespace FluidSimulationGPU {
public class FluidParcelsManager {
    enum ComputeKernel {
        Init,
        TransferVelocities,
        UpdateAffineState,
        Advect
    }

    struct ParcelsData {
        public float mass;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 cx;
        public Vector2 cy;
    }

    static readonly int[] computeKernels = (int[])Enum.GetValues(typeof(ComputeKernel));

    ComputeShader compute;
    public ComputeBuffer parcelsData;

    public int Count { get; set; }
    public float CollisionDampingFactor { get; set; }

    public FluidParcelsManager(int count, ComputeShader compute) {
        Count = count < 1 ? 1 : count;
        CollisionDampingFactor = 0.1f;
        this.compute = compute;

        CreateBuffers();
        ComputeHelper.SetBuffer(compute, parcelsData, "parcelsData", computeKernels);
        ComputeHelper.Dispatch(compute, count, 1, ComputeKernel.Init);
    }

    public void Setup(FluidGridManager gridManager) {
        CreateBuffers();
        BindBuffers(gridManager);
        BindSettings(gridManager);
    }

    void CreateBuffers() {
        ComputeHelper.CreateStructuredBuffer<ParcelsData>(ref parcelsData, Count);
    }

    void BindBuffers(FluidGridManager gridManager) {
        ComputeHelper.SetBuffer(compute, parcelsData, "parcelsData", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.cellType, "cellType", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.velocityMap, "velocityMap", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.velocityMap, "velocityMapSample", computeKernels);
    }

    void BindSettings(FluidGridManager gridManager) {
        compute.SetInt("count", Count);
        compute.SetFloat("collisionDampingFactor", CollisionDampingFactor);
        compute.SetInts("gridResolution", gridManager.resolution.x, gridManager.resolution.y);
    }

    public void TransferGridData(FluidGridManager gridManager) {
        ComputeHelper.Dispatch(compute, Count, 1, ComputeKernel.TransferVelocities);
    }

    public void UpdateAffineState(FluidGridManager gridManager) {
        ComputeHelper.Dispatch(compute, Count, 1, ComputeKernel.UpdateAffineState);
    }

    public void Advect(FluidGridManager gridManager, float dt) {
        compute.SetFloat("dt", dt);
        ComputeHelper.Dispatch(compute, Count, 1, ComputeKernel.Advect);
    }

    public void ReleaseBuffers() {
        ComputeHelper.Release(parcelsData);
    }
}
}
