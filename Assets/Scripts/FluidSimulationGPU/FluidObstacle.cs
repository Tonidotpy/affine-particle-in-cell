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

    public Vector3 Origin { get; set; }

    public int GetMeshVertexCount => mesh.vertices.Length;
    public int GetMeshTriangleCount => mesh.triangles.Length;

    void Start() {
        mesh = GetComponent<MeshFilter>().mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        Origin = Vector2.zero;
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
