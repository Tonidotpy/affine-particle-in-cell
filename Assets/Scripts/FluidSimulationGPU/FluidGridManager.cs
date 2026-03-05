using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Seb.Helpers;
using System;
using static UnityEngine.Mathf;

namespace FluidSimulationGPU {
public class FluidGridManager {
    enum ComputeKernel {
        PreparePressureSolver,
        RunPressureSolver,
    }

    ComputeShader compute;
    static readonly int[] computeKernels = (int[])Enum.GetValues(typeof(ComputeKernel));

    public readonly Vector2Int resolution = new(50, 35);

    public float sorMultiplier = 1.7f;
    public float density = 1.3f;            // kg/m^2
    public float ambientTemperature = 300f; // K
    public float smokeBuoyancyMultiplier = 0.3f;
    public float temperatureBuoyancyMultiplier = 1f;

    RenderTexture cellType;
    RenderTexture velocityMap;
    RenderTexture velocityMapAdvected;
    RenderTexture pressureMap;
    RenderTexture pressureSolverData;
    RenderTexture temperatureMap;
    RenderTexture smokeMap;

    public FluidGridManager(int width, int height, ComputeShader compute) {
        resolution = new(width, height);
        this.compute = compute;
    }

    public void Setup() {
        CreateTextures();
        BindTextures();
        BindSettings();
    }

    void CreateTextures() {
        ComputeHelper.CreateRenderTexture(ref cellType, resolution.x, resolution.y, FilterMode.Point, GraphicsFormat.R8_UInt);
        ComputeHelper.CreateRenderTexture(ref velocityMap, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32G32_SFloat);
        ComputeHelper.CreateRenderTexture(ref velocityMapAdvected, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32G32_SFloat);
        ComputeHelper.CreateRenderTexture(ref pressureMap, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32_SFloat);
        ComputeHelper.CreateRenderTexture(ref pressureSolverData, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32G32_SFloat);
        ComputeHelper.CreateRenderTexture(ref temperatureMap, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32_SFloat);
        ComputeHelper.CreateRenderTexture(ref smokeMap, resolution.x, resolution.y, FilterMode.Bilinear, GraphicsFormat.R32_SFloat);
    }

    void BindTextures() {
        ComputeHelper.SetTexture(compute, cellType, "cellType", computeKernels);
        ComputeHelper.SetTexture(compute, velocityMap, "velocityMap", computeKernels);
        ComputeHelper.SetTexture(compute, velocityMapAdvected, "velocityMapAdvected", computeKernels);
        ComputeHelper.SetTexture(compute, pressureMap, "pressureMap", computeKernels);
        ComputeHelper.SetTexture(compute, pressureSolverData, "pressureSolverData", computeKernels);
        ComputeHelper.SetTexture(compute, temperatureMap, "temperatureMap", computeKernels);
        ComputeHelper.SetTexture(compute, smokeMap, "smokeMap", computeKernels);
    }

    void BindSettings() {
        compute.SetInts("resolution", resolution.x, resolution.y);
    }

    /// <summary>
    /// Calculate pressure values needed to remove divergence of fluid
    /// using the Gauss-Seidel method with SOR
    /// </summary>
    /// <param name="iterations">Total number of iterations of the pressure solver</param>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void SolvePressure(int iterations, float dt) {
        compute.SetFloat("dt", dt);
        compute.SetFloat("density", density);
        compute.SetFloat("sor", sorMultiplier);

        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.PreparePressureSolver);

        for (int i = 0; i < iterations; ++i) {
            compute.SetFloat("solverPassIndex", i);
            ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.RunPressureSolver);
        }
    }

    public void ReleaseTextures() {
        ComputeHelper.Release(
            cellType,
            velocityMap,
            velocityMapAdvected,
            pressureMap,
            pressureSolverData,
            temperatureMap,
            smokeMap);
    }
}
}
