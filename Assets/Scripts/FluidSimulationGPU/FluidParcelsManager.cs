using UnityEngine;
using System;
using Seb.Helpers;

namespace FluidSimulationGPU {
public class FluidParcelsManager {
    enum ComputeKernel {
        Init,
        TransferVelocities,
        UpdateAffineState,
        Advect,
        BitonicSortStep
    }

    struct ParcelsData {
        public int toRemove;
        public float mass;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 cx;
        public Vector2 cy;
    }

    static readonly int[] computeKernels = (int[])Enum.GetValues(typeof(ComputeKernel));

    int count;
    int maxCount;

    ComputeShader compute;
    public ComputeBuffer parcelsData;
    ComputeBuffer parcelsToRemove;

    public int Count {
        get { return count; }
    }
    public int MaxCount {
        get { return maxCount; }
        set { maxCount = Mathf.NextPowerOfTwo(value); }
    }
    public float CollisionDampingFactor { get; set; }

    public FluidParcelsManager(int maxCount, ComputeShader compute) {
        this.maxCount = maxCount < 1 ? 1 : Mathf.NextPowerOfTwo(maxCount);
        count = maxCount;
        CollisionDampingFactor = 0.1f;
        this.compute = compute;

        CreateBuffers();
        compute.SetInt("powerOfTwoCount", MaxCount);
        compute.SetInt("count", Count);
        ComputeHelper.SetBuffer(compute, parcelsToRemove, "parcelsToRemove", computeKernels);
        ComputeHelper.SetBuffer(compute, parcelsData, "parcelsData", computeKernels);
        ComputeHelper.Dispatch(compute, maxCount, 1, ComputeKernel.Init);
    }

    public void Setup(FluidGridManager gridManager) {
        CreateBuffers();
        BindBuffers(gridManager);
        BindSettings(gridManager);
    }

    void CreateBuffers() {
        ComputeHelper.CreateStructuredBuffer<ParcelsData>(ref parcelsData, maxCount);
        ComputeHelper.CreateStructuredBuffer<int>(ref parcelsToRemove, 1);
    }

    void BindBuffers(FluidGridManager gridManager) {
        ComputeHelper.SetBuffer(compute, parcelsData, "parcelsData", computeKernels);
        ComputeHelper.SetBuffer(compute, parcelsToRemove, "parcelsToRemove", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.cellType, "cellType", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.velocityMap, "velocityMap", computeKernels);
        ComputeHelper.SetTexture(compute, gridManager.velocityMap, "velocityMapSample", computeKernels);
    }

    void BindSettings(FluidGridManager gridManager) {
        compute.SetInt("powerOfTwoCount", maxCount);
        compute.SetInt("count", Count);
        compute.SetFloat("collisionDampingFactor", CollisionDampingFactor);
        compute.SetInts("gridResolution", gridManager.resolution.x, gridManager.resolution.y);
    }

    public void TransferGridData(FluidGridManager gridManager) {
        if (count <= 0)
            return;
        ComputeHelper.Dispatch(compute, count, 1, ComputeKernel.TransferVelocities);
    }

    public void UpdateAffineState(FluidGridManager gridManager) {
        if (count <= 0)
            return;
        ComputeHelper.Dispatch(compute, count, 1, ComputeKernel.UpdateAffineState);
    }

    public void Advect(FluidGridManager gridManager, float dt) {
        if (count <= 0)
            return;
        compute.SetFloat("dt", dt);
        ComputeHelper.Dispatch(compute, count, 1, ComputeKernel.Advect);
    }

    void BitonicSort(int bufferSize) {
        for (int k = 2; k <= bufferSize; k <<= 1) {
            for (int j = k >> 1; j > 0; j >>= 1) {
                compute.SetInt("bitonicMajorStepIndex", k);
                compute.SetInt("bitonicMinorStepIndex", j);
                ComputeHelper.Dispatch(compute, bufferSize, 1, ComputeKernel.BitonicSortStep);
            }
        }
    }

    public void RemoveParcelsOutsideGridBounds() {
        if (count <= 0)
            return;
        // Sort by to remove flag
        BitonicSort(maxCount);

        // Update Parcels count
        int[] aux = new int[1];
        parcelsToRemove.GetData(aux);
        int removed = aux[0];

        count = Math.Max(0, count - removed);
        compute.SetInt("count", Count);
    }

    public void ReleaseBuffers() {
        ComputeHelper.Release(parcelsData, parcelsToRemove);
    }
}
}
