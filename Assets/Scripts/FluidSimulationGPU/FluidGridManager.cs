using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Seb.Helpers;
using System;
using static UnityEngine.Mathf;

namespace FluidSimulationGPU {
public class FluidGridManager {
    enum ComputeKernel {
        Init,
        AdvectVelocities,
        VelocityAdvectionReadback,
        AdvectSmoke,
        SmokeAdvectionReadback,
        AddBuoyancyForce,
        PreparePressureSolver,
        RunPressureSolver,
        UpdateVelocities,
        HandleInput,
        UpdateObstacles,
        UpdateObstacleEdges,
        UpdateSmokeSources,
        ClearObstacles
    }

    /// <summary>
    /// Data needed to solve the pressure equation using the Gauss-Seidel method
    /// </summary>
    public struct PressureSolverData {
        // flowData is made out of multiple variables compressed into a single
        // integer for optimization purposes
        //
        // The format is the following:
        //     [31     ] - flowTop: 0 if there is no flow from the top cell 1 otherwise
        //     [30     ] - flowBottom: 0 if there is no flow from the bottom cell 1 otherwise
        //     [29     ] - flowRight: 0 if there is no flow from the right cell 1 otherwise
        //     [28     ] - flowLeft: 0 if there is no flow from the top cell 1 otherwise
        //     [27 -  8] - Reserved
        //     [ 7 -  0] - flowEdgeCount: number of edges with flows different from 0
        public uint flowData;
        public float velocityTerm;
    }

    ComputeShader compute;
    static readonly int[] computeKernels = (int[])Enum.GetValues(typeof(ComputeKernel));

    public readonly Vector2Int resolution = new(50, 35);

    public float sorMultiplier = 1.7f;
    public Vector2 gravity = new Vector2(0, -9.81f); // m/s^2
    public float density = 1.3f;                     // kg/m^2
    public float ambientTemperature = 300f;          // K
    public float smokeDiffusionMultiplier = 1.2f;
    public float smokeDecayMultiplier = 0.05f;
    public float smokeBuoyancyMultiplier = 1f;
    public float temperatureDiffusionMultiplier = 1.2f;
    public float temperatureDecayMultiplier = 0.05f;
    public float temperatureBuoyancyMultiplier = 1f;

    public RenderTexture debugMap;
    public RenderTexture cellType;
    public RenderTexture velocityMap;
    public RenderTexture velocityMapAdvected;
    public RenderTexture pressureMap;
    public ComputeBuffer pressureSolverData;
    public RenderTexture temperatureMap;
    public RenderTexture smokeMap;
    public RenderTexture smokeMapAdvected;

    public FluidObstacle[] obstacles;
    public ComputeBuffer obstacleData;
    public ComputeBuffer obstacleIndices;
    public ComputeBuffer obstacleVertices;
    public ComputeBuffer obstacleTriangles;

    public FluidGridManager(int width, int height, ComputeShader compute) {
        resolution = new(width, height);
        this.compute = compute;

        obstacles = Array.Empty<FluidObstacle>();

        Setup();
        ComputeHelper.Dispatch(compute, width, height, ComputeKernel.Init);
    }

    public void Setup() {
        CreateTextures();
        BindTextures();
        BindSettings();
    }

    void CreateTextures() {
        ComputeHelper.CreateRenderTexture(ref debugMap, resolution.x, resolution.y, FilterMode.Point,
                                          GraphicsFormat.R32G32B32A32_SFloat);
        // Cell type render texture format uses float because integers do not
        // sample correctly inside shaders for wathever reasons
        ComputeHelper.CreateRenderTexture(ref cellType, resolution.x, resolution.y, FilterMode.Point,
                                          GraphicsFormat.R32G32B32A32_SFloat);
        ComputeHelper.CreateRenderTexture(ref velocityMap, resolution.x, resolution.y, FilterMode.Bilinear,
                                          GraphicsFormat.R32G32_SFloat);
        ComputeHelper.CreateRenderTexture(ref velocityMapAdvected, resolution.x, resolution.y,
                                          FilterMode.Bilinear, GraphicsFormat.R32G32_SFloat);
        ComputeHelper.CreateRenderTexture(ref pressureMap, resolution.x, resolution.y, FilterMode.Bilinear,
                                          GraphicsFormat.R32_SFloat);
        ComputeHelper.CreateStructuredBuffer<PressureSolverData>(ref pressureSolverData, resolution.x * resolution.y);
        ComputeHelper.CreateRenderTexture(ref temperatureMap, resolution.x, resolution.y, FilterMode.Bilinear,
                                          GraphicsFormat.R32_SFloat);
        ComputeHelper.CreateRenderTexture(ref smokeMap, resolution.x, resolution.y, FilterMode.Bilinear,
                                          GraphicsFormat.R32G32B32A32_SFloat);
        ComputeHelper.CreateRenderTexture(ref smokeMapAdvected, resolution.x, resolution.y, FilterMode.Bilinear,
                                          GraphicsFormat.R32G32B32A32_SFloat);
    }

    void BindTextures() {
        ComputeHelper.SetTexture(compute, debugMap, "debugMap", computeKernels);
        ComputeHelper.SetTexture(compute, cellType, "cellType", computeKernels);
        ComputeHelper.SetTexture(compute, velocityMap, "velocityMap", computeKernels);
        ComputeHelper.SetTexture(compute, velocityMap, "velocityMapSample", computeKernels);
        ComputeHelper.SetTexture(compute, velocityMapAdvected, "velocityMapAdvected", computeKernels);
        ComputeHelper.SetTexture(compute, pressureMap, "pressureMap", computeKernels);
        ComputeHelper.SetBuffer(compute, pressureSolverData, "pressureSolverData", computeKernels);
        ComputeHelper.SetTexture(compute, temperatureMap, "temperatureMap", computeKernels);
        ComputeHelper.SetTexture(compute, smokeMap, "smokeMap", computeKernels);
        ComputeHelper.SetTexture(compute, smokeMap, "smokeMapSample", computeKernels);
        ComputeHelper.SetTexture(compute, smokeMapAdvected, "smokeMapAdvected", computeKernels);
    }

    void BindSettings() {
        compute.SetInts("resolution", resolution.x, resolution.y);
        compute.SetFloat("ambientTemperature", ambientTemperature);
    }

    /// <summary>
    /// Advect velocities using the Semi-Lagrangian method
    /// In a Semi-Lagrangian method we can imagine a particle traveling at a
    /// certain velocity landing on the Cell borders.
    /// Since we know the final position and velocity of the "virtual particle"
    /// via interpolation we can calculate its previous position given the
    /// simulation time step
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AdvectVelocities(float dt) {
        compute.SetFloat("dt", dt);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.AdvectVelocities);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.VelocityAdvectionReadback);
    }

    /// <summary>
    /// Advect smoke using the Semi-Lagrangian method
    /// In a Semi-Lagrangian method we can imagine a particle traveling at a
    /// certain velocity landing on the Cell center.
    /// Since we know the final position and velocity of the "virtual particle"
    /// via interpolation we can calculate its previous position given the
    /// simulation time step
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AdvectSmoke(float dt) {
        compute.SetFloat("dt", dt);
        compute.SetFloat("smokeDiffusionMultiplier", smokeDiffusionMultiplier);
        compute.SetFloat("smokeDecayMultiplier", smokeDecayMultiplier);
        compute.SetFloat("temperatureDiffusionMultiplier", temperatureDiffusionMultiplier);
        compute.SetFloat("temperatureDecayMultiplier", temperatureDecayMultiplier);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.AdvectSmoke);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.SmokeAdvectionReadback);
    }

    /// <summary>
    /// Add smoke from smoke sources
    /// </summary>
    public void AddSmokeFromSources(float dt) {
        compute.SetFloat("dt", dt);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.UpdateSmokeSources);
    }

    /// <summary>
    /// Add buoyancy force to the fluid
    /// </summary>
    /// <param name="dt">Time difference between two simulation steps in seconds</param>
    public void AddBuoyancyForce(float dt) {
        compute.SetFloat("dt", dt);
        compute.SetVector("gravity", gravity);
        compute.SetFloat("smokeBuoyancyMultiplier", smokeBuoyancyMultiplier);
        compute.SetFloat("temperatureBuoyancyMultiplier", temperatureBuoyancyMultiplier);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.AddBuoyancyForce);
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
        for (int i = 0; i < iterations * 2; ++i) {
            compute.SetFloat("solverPassIndex", i % 2);
            ComputeHelper.Dispatch(compute, resolution.x * resolution.y / 2, ComputeKernel.RunPressureSolver);
        }
    }

    public void UpdateVelocities(float dt) {
        compute.SetFloat("dt", dt);
        compute.SetFloat("density", density);
        ComputeHelper.Dispatch(compute, resolution.x + 1, resolution.y + 1, ComputeKernel.UpdateVelocities);
    }

    /// <summary>
    /// Set a specific velocity amount at a given position inside a circle in the Grid
    /// </summary>
    /// <param name="center">Coordinates where the velocity is set</param>
    /// <param name="radius">Radius of the circle where the velocity is set</param>
    /// <param name="velocity">The velocity to set</param>
    public void SetVelocityAtPosition(Vector2 center, float radius, Vector2 velocity) {
        compute.SetBool("inputShouldSetVelocity", true);
        compute.SetVector("inputVelocityPosition", center);
        compute.SetFloat("inputVelocityRadius", radius);
        compute.SetVector("inputVelocity", velocity);
    }

    /// <summary>
    /// Add a specific amount of smoke at a given position inside a circle in the Grid
    /// </summary>
    /// <param name="center">Coordinates where the smoke is added</param>
    /// <param name="radius">Radius of the circle where the smoke is added</param>
    /// <param name="amount">Amount of smoke to add</param>
    /// <param name="color">Smoke color</param>
    /// <param name="temperature">Smoke temperature in K</param>
    public void AddSmokeAtPosition(Vector2 center, float radius, float amount, Color color, float temperature) {
        compute.SetBool("inputShouldAddSmoke", true);
        compute.SetVector("inputSmokePosition", center);
        compute.SetFloat("inputSmokeRadius", radius);
        compute.SetFloat("inputSmokeAmount", amount);
        compute.SetVector("inputSmokeColor", color);
        compute.SetFloat("inputSmokeTemperature", temperature);
    }

    /// <summary>
    /// Set all velocity values to 0
    /// </summary>
    public void ClearVelocities() {
        compute.SetBool("inputShouldClearVelocity", true);
    }

    /// <summary>
    /// Handle user input
    /// </summary>
    public void HandleInput() {
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.HandleInput);
        compute.SetBool("inputShouldSetVelocity", false);
        compute.SetBool("inputShouldClearVelocity", false);
        compute.SetBool("inputShouldAddSmoke", false);
    }

    public void UpdateObstacles() {
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.ClearObstacles);
        int count = obstacles.Length;
        if (count == 0)
            return;

        int vertexCount = 0;
        int triangleCount = 0;
        Array.ForEach(obstacles, obstacle => {
            vertexCount += obstacle.GetMeshVertexCount;
            triangleCount += obstacle.GetMeshTriangleCount;
        });

        ComputeHelper.CreateStructuredBuffer<FluidObstacle.ObstacleData>(ref obstacleData, count, mode: ComputeBufferMode.SubUpdates);
        ComputeHelper.CreateStructuredBuffer<FluidObstacle.MeshDataIndex>(ref obstacleIndices, count + 1, mode: ComputeBufferMode.SubUpdates);
        ComputeHelper.CreateStructuredBuffer<Vector2>(ref obstacleVertices, vertexCount);
        ComputeHelper.CreateStructuredBuffer<int>(ref obstacleTriangles, triangleCount);

        compute.SetInt("obstacleCount", count);
        ComputeHelper.SetBuffer(compute, obstacleData, "obstacleData", computeKernels);
        ComputeHelper.SetBuffer(compute, obstacleIndices, "obstacleIndices", computeKernels);
        ComputeHelper.SetBuffer(compute, obstacleVertices, "obstacleVertices", computeKernels);
        ComputeHelper.SetBuffer(compute, obstacleTriangles, "obstacleTriangles", computeKernels);

        var data = obstacleData.BeginWrite<FluidObstacle.ObstacleData>(0, count);
        var indices = obstacleIndices.BeginWrite<FluidObstacle.MeshDataIndex>(0, count + 1);

        int vertexIndex = 0;
        int triangleIndex = 0;
        for (int i = 0; i < count; ++i) {
            data[i] = obstacles[i].GetObstacleData();
            FluidObstacle.MeshData meshData = obstacles[i].GetMeshData();

            // Set the index of the first vertex and triangle ID of the current obstacle
            indices[i] = new FluidObstacle.MeshDataIndex {
                vertex = vertexIndex,
                triangle = triangleIndex
            };

            int vertexLength = meshData.vertices.Length;
            int triangleLength = meshData.triangles.Length;
            obstacleVertices.SetData(meshData.vertices, 0, vertexIndex, vertexLength);
            obstacleTriangles.SetData(meshData.triangles, 0, triangleIndex, triangleLength);

            vertexIndex += vertexLength;
            triangleIndex += triangleLength;
        }

        indices[count] = new FluidObstacle.MeshDataIndex {
            vertex = vertexIndex,
            triangle = triangleIndex
        };
        obstacleData.EndWrite<FluidObstacle.ObstacleData>(count);
        obstacleIndices.EndWrite<FluidObstacle.MeshDataIndex>(count + 1);

        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.UpdateObstacles);
        ComputeHelper.Dispatch(compute, resolution.x, resolution.y, ComputeKernel.UpdateObstacleEdges);
    }

    public void ReleaseTextures() {
        ComputeHelper.Release(pressureSolverData, obstacleIndices, obstacleVertices, obstacleTriangles);
        ComputeHelper.Release(debugMap, cellType, velocityMap, velocityMapAdvected, pressureMap, temperatureMap,
                              smokeMap, smokeMapAdvected);
    }
}
}
