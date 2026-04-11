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
        Velocity,
        Divergence,
        Pressure,
        Temperature,
        Smoke,
    }

    /// <summary>
    /// Which channel of the velocity texture should be rendered
    /// </summary>
    public enum VelocityChannel {
        Horizontal,
        Vertical,
        Both
    }

    MeshRenderer meshRenderer = null;
    Material material = null;
    FluidGridManager grid = null;

    public Shader shader;
    public VisualizationMode visualizationMode = VisualizationMode.Debug;

    [Header("Input")]
    public float inputRadius = 15f;
    public Color inputColor = new Color(1f, 1f, 1f, 0.3f);
    public Color inputActiveColor = new Color(0f, 1f, 0.1f, 0.3f);

    [Header("Grid settings")]
    [Min(0.1f)]
    public float cellSize = 1f;

    [Header("Velocity")]
    public float velocityDisplayRange = 1f;
    public VelocityChannel velocityChannel = VelocityChannel.Both;

    [Header("Divergence")]
    public float divergenceDisplayRange = 1f;
    public Color negativeDivergenceColor = new Color(0.3f, 0.3f, 1f, 1f);
    public Color positiveDivergenceColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Pressure")]
    public float pressureDisplayRange = 1f;
    public Color negativePressureColor = new Color(0.3f, 1f, 0.3f, 1f);
    public Color positivePressureColor = new Color(0.7f, 0.3f, 0.7f, 1f);

    [Header("Temperature")]
    public float temperatureDisplayRange = 1f;
    public Color negativeTemperatureColor = new Color(0f, 0f, 1f, 1f);
    public Color positiveTemperatureColor = new Color(1f, 0f, 0f, 1f);

    [Header("Smoke")]
    public float smokeDisplayRange = 1f;
    public Color smokeColor = new Color(1f, 1f, 1f, 1f);

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

        // Velocity
        material.SetTexture("velocityMap", grid.velocityMap);
        material.SetInteger("velocityChannel", (int)velocityChannel);
        material.SetFloat("velocityDisplayRange", velocityDisplayRange);

        // Divergence
        material.SetFloat("divergenceDisplayRange", divergenceDisplayRange);
        material.SetVector("negativeDivergenceColor", negativeDivergenceColor);
        material.SetVector("positiveDivergenceColor", positiveDivergenceColor);

        // Pressure
        material.SetTexture("pressureMap", grid.pressureMap);
        material.SetFloat("pressureDisplayRange", pressureDisplayRange);
        material.SetVector("negativePressureColor", negativePressureColor);
        material.SetVector("positivePressureColor", positivePressureColor);

        // Temperature
        material.SetFloat("temperatureDisplayRange", temperatureDisplayRange);
        material.SetVector("negativeTemperatureColor", negativeTemperatureColor);
        material.SetVector("positiveTemperatureColor", positiveTemperatureColor);

        // Smoke
        material.SetTexture("smokeMap", grid.smokeMap);
        material.SetFloat("smokeDisplayRange", smokeDisplayRange);
    }

    public void RenderInput(Vector2 mousePosition, bool isMousePressed) {
        Draw.StartLayerIfNotInMatching(Vector2.zero, 1, false);
        Draw.Point(mousePosition, inputRadius, isMousePressed ? inputActiveColor : inputColor);
    }

    public void Update() {
        if (grid != null) {
            SetupShader();
            UpdateShaderData();
        }

        Draw.StartLayerIfNotInMatching(Vector2.zero, 1, true);
        Draw.Text(FontType.JetbrainsMonoRegular, $"Mode: {visualizationMode}", 20f,
                  new Vector2(30f, Screen.height - 20f), Anchor.TopLeft, Color.white);
    }

    /// <summary>
    /// Convert coordinates from the fluid Grid reference system to the Unity
    /// world reference system.
    /// </summary>
    /// <param name="i">Horizontal coordinate in Grid space</param>
    /// <param name="j">Vertical coordinate in Grid space</param>
    /// <returns>Converted coordinates in world space</returns>
    public Vector2 CellCenterToWorld(int i, int j) {
        return CellCenterToWorld((float)i, (float)j);
    }
    public Vector2 CellCenterToWorld(float x, float y) {
        return new Vector2(x - (grid.resolution.x - 1) * 0.5f, y - (grid.resolution.y - 1) * 0.5f) * cellSize;
    }

    /// <summary>
    /// Convert coordinates from the Uniy world reference system to the fluid
    /// Grid reference system.
    /// </summary>
    /// <param name="position">Coordinate in world space</param>
    /// <returns>Converted coordinates in Grid space</returns>
    public Vector2Int WorldToCellCenter(Vector2 position) {
        Vector2 cell = position / cellSize;
        cell.x += (grid.resolution.x - 1) * 0.5f;
        cell.y += (grid.resolution.y - 1) * 0.5f;
        return Vector2Int.RoundToInt(cell);
    }

    /// <summary>
    /// Set the fluid Grid to render
    /// </summary>
    /// <param name="grid">The Grid to render</param>
    public void SetGridToRender(FluidGridManager grid) {
        this.grid = grid;
    }

    /// <summary>
    /// Cycle through visualization modes
    /// </summary>
    /// <param name="isForward">Change to the next visualization mode if its true, to the previous if false</param>
    public void CycleVisualizationMode(bool isForward) {
        var modeCount = VisualizationMode.GetNames(typeof(VisualizationMode)).Length;
        int direction = isForward ? -1 : 1;

        int mode = (int)visualizationMode + direction;
        if (mode < 0)
            mode += modeCount;
        mode %= modeCount;
        visualizationMode = (VisualizationMode)mode;
    }
}
}
