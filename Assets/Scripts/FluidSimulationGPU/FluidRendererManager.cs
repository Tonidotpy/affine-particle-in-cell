using UnityEngine;
using Seb.Vis;

namespace FluidSimulationGPU {
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FluidRendererManager : MonoBehaviour {
    /// <summary>
    /// List of different rendering modes each one to show a specific
    /// characteristic of the fluid.
    /// </summary>
    public enum VisualizationMode {
        Debug,
        Pressure,
    }

    MeshRenderer meshRenderer = null;
    Material material = null;
    FluidGridManager grid = null;

    public Shader shader;
    public VisualizationMode visualizationMode = VisualizationMode.Debug;

    [Header("Grid settings")]
    [Min(0.1f)]
    public float cellSize = 1f;

    [Header("Pressure")]
    public float pressureDisplayRange = 1f;
    public Color negativePressureColor = new Color(0.3f, 1f, 0.3f, 0.7f);
    public Color positivePressureColor = new Color(0.7f, 0.3f, 0.7f, 0.7f);

    public Vector2 WorldSize => (Vector2)grid.resolution * cellSize;

    void SetupShader() {
        // Fallback to diffuse shader if null
        if (shader == null) {
            Debug.LogWarning("Missing shader fallback to diffuse");
            shader = Shader.Find("Transparent/Diffuse");
        }

        // Get mesh renderer
        if (meshRenderer == null) {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.transform.localScale = new Vector3(WorldSize.x, WorldSize.y, 1);
        }

        // Create material using selected shader
        if (material == null || material.shader != shader) {
            material = new Material(shader);
            meshRenderer.sharedMaterial = material;
        }
    }

    void UpdateShaderData() {
        material.SetVector("resolution", (Vector2)grid.resolution);
        material.SetInteger("visualizationMode", (int)visualizationMode);
        material.SetTexture("debugMap", grid.debugMap);

        // Pressure
        material.SetTexture("pressureMap", grid.pressureMap);
        material.SetFloat("pressureDisplayRange", pressureDisplayRange);
        material.SetVector("negativePressureColor", negativePressureColor);
        material.SetVector("positivePressureColor", positivePressureColor);
    }

    void RenderInput() {
        // TODO: Render input
        // Draw.StartLayerIfNotInMatching(Vector2.zero, 1, false);
        // Draw.Point(mousePosition, inputRadius, isMousePressed ? inputActiveColor : inputColor);
    }

    public void Update() {
        if (grid != null) {
            SetupShader();
            UpdateShaderData();
        }

        Draw.StartLayerIfNotInMatching(Vector2.zero, 1, true);
        Draw.Text(FontType.JetbrainsMonoRegular, $"Mode: {visualizationMode}", 20f, new Vector2(30f, Screen.height - 20f), Anchor.TopLeft, Color.white);
    }

    /// <summary>
    /// Set the fluid Grid to render
    /// </summary>
    /// <param name="grid">The Grid to render</param>
    public void SetGridToRender(FluidGridManager grid) {
        this.grid = grid;
    }
}
}
