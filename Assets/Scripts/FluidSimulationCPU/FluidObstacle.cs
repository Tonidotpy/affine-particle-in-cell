using UnityEngine;

namespace FluidSimulationCPU {
/// <summary>
/// Script used to render any GameObject with a Mesh onto the fluid Grid as a
/// solid obstacle.
/// Works also for 3D objects but only their projection on the 2D Grid is rendered.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FluidObstacle : MonoBehaviour {
    Mesh mesh;
    MeshRenderer meshRenderer;

    /// <summary>
    /// Get the Axis-Aligned Bounding Box (AABB) of the obstacle.
    /// The <c>meshRenderer</c> is used instead of the <c>mesh</c> bounds since
    /// the latter is not affected by any transformation (e.g. rotation).
    /// </summary>
    public Bounds bounds {
        get { return meshRenderer.bounds; }
    }

    void Start() {
        mesh = GetComponent<MeshFilter>().mesh;
        meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Check if a given point is contained inside this mesh
    /// The GameObject is supposed to have a MeshFilter component with a mesh
    /// and the mesh is assumed to be 2D oriented correctly such as its normal
    /// points towards the Camera
    /// </summary>
    /// <param name="point">The coordinates of the point to check</param>
    /// <returns>True if the point lies inside the object, false otherwise</returns>
    public bool Contains(Vector2 point) {
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        int[] triangles = mesh.triangles;
        Vector3[] localVertices = mesh.vertices;
        Vector3[] vertices = new Vector3[localVertices.Length];
        for (int i = 0; i < localVertices.Length; ++i) {
            vertices[i] = localToWorld.MultiplyPoint3x4(localVertices[i]);
        }

        for (int i = 0; i < triangles.Length; i += 3) {
            Vector2 v0 = vertices[triangles[i]];
            Vector2 v1 = vertices[triangles[i + 1]];
            Vector2 v2 = vertices[triangles[i + 2]];

            // Calculate the barycentric coordinates of point P with respect to triangle [v0,v1,v2]
            float denominator = ((v1.y - v2.y) * (v0.x - v2.x) + (v2.x - v1.x) * (v0.y - v2.y));
            float a = ((v1.y - v2.y) * (point.x - v2.x) + (v2.x - v1.x) * (point.y - v2.y)) / denominator;
            float b = ((v2.y - v0.y) * (point.x - v2.x) + (v0.x - v2.x) * (point.y - v2.y)) / denominator;
            float c = 1 - a - b;

            // Check if all barycentric coordinates are non-negative
            if (a >= 0 && b >= 0 && c >= 0)
                return true;
        }
        return false;
    }
}
}
