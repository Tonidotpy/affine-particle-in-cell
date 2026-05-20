using UnityEngine;
using System;
using Seb.Helpers;

namespace FluidSimulationGPU {
public class FluidParcelsManager {
    enum ComputeKernel {
        Init
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

    public FluidParcelsManager(int count, ComputeShader compute) {
        Count = count < 1 ? 1 : count;
        this.compute = compute;

        Setup();
        ComputeHelper.Dispatch(compute, count, 1, ComputeKernel.Init);
    }

    public void Setup() {
        CreateBuffers();
        BindBuffers();
        BindSettings();
    }

    void CreateBuffers() {
        ComputeHelper.CreateStructuredBuffer<ParcelsData>(ref parcelsData, Count);
    }

    void BindBuffers() {
        ComputeHelper.SetBuffer(compute, parcelsData, "parcelsData", computeKernels);
    }

    void BindSettings() {
        compute.SetInt("count", Count);
    }

    public void ReleaseBuffers() {
        ComputeHelper.Release(parcelsData);
    }
}
}
