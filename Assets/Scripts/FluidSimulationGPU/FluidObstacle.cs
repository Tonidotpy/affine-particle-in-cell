using UnityEngine;

namespace FluidSimulationGPU {
/// <summary>
/// Script used to render any GameObject with a Mesh onto the fluid Grid as a
/// solid obstacle.
/// Works also for 3D objects but only their projection on the 2D Grid is rendered.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FluidObstacle : MonoBehaviour {
    public struct ObstacleData {
        public bool isSmokeSource;
        public Vector2 velocityRate;
        public float smokeRateMultiplier;
        public Vector3 smokeRate;
        public float temperature;
    }

    public struct MeshData {
        public Vector2[] vertices;
        public int[] triangles;
    }

    public struct MeshDataIndex {
        public int vertex;
        public int triangle;
    }

    Mesh mesh;
    MeshRenderer meshRenderer;

    public bool isSmokeSource;
    public Vector2 velocityRate;
    [Min(0f)]
    public float smokeRateMultiplier = 1;
    public Vector3 smokeRate; // Amount of smoke per second
    public float temperature = 25; // °C

    public Vector3 Origin { get; set; }

    /// <summary>
    /// Get or set the temperature in K
    /// </summary>
    public float Temperature {
        get { return temperature + 273.15f; }
        set { temperature = Mathf.Max(value - 273.15f, -273.15f); }
    }

    public int GetMeshVertexCount => mesh.vertices.Length;
    public int GetMeshTriangleCount => mesh.triangles.Length;

    void Start() {
        mesh = GetComponent<MeshFilter>().mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        Origin = Vector2.zero;
    }

    public ObstacleData GetObstacleData() {
        return new ObstacleData {
            isSmokeSource = isSmokeSource,
            velocityRate = velocityRate,
            smokeRateMultiplier = smokeRateMultiplier,
            smokeRate = smokeRate,
            temperature = Temperature, // !!! Use K instead of °C !!!
        };
    }

    public MeshData GetMeshData() {
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        int[] triangles = mesh.triangles;
        Vector3[] localVertices = mesh.vertices;
        Vector2[] vertices = new Vector2[localVertices.Length];
        for (int i = 0; i < localVertices.Length; ++i) {
            vertices[i] = localToWorld.MultiplyPoint3x4(localVertices[i]) + Origin;
        }

        return new MeshData {
            vertices = vertices,
            triangles = triangles
        };
    }
}
}
