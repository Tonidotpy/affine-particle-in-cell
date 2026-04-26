using UnityEngine;
using Seb.Helpers;
using Seb.Vis;
using static UnityEngine.Mathf;

namespace FluidSimulationCPU {
/// <summary>
/// Render for the fluid Grid.
/// The renderer uses Unity world coordinates as main reference system and it
/// implements specific functions to map world coordinates to the Grid coordinates.
/// </summary>
public class FluidRenderer : MonoBehaviour {
    /// <summary>
    /// List of different rendering modes each one to show a specific
    /// characteristic of the fluid.
    /// </summary>
    public enum VisualizationMode {
        None,
        Velocity,
        VelocityMap,
        Divergence,
        Pressure,
        Temperature,
        Smoke,
    }

    FluidParcels parcels;
    FluidGridMac grid;
    bool isRenderingEnabled;

    public VisualizationMode visualizationMode = VisualizationMode.Smoke;
    public float fontSize = 0.18f;

    [Header("Input")]
    public float inputRadius = 0.5f;
    public Color inputColor = new Color(1f, 1f, 1f, 0.3f);
    public Color inputActiveColor = new Color(0f, 1f, 0.1f, 0.3f);

    [Header("Grid settings")]
    [Min(0.1f)]
    public float CellSize = 1f;
    [Range(0, 0.2f)]
    public float cellBorderThickness = 0.05f;
    public Color gridColor = new Color(0.2156862745f, 0.2156862745f, 0.2156862745f);

    [Header("Parcels settings")]
    public bool showParcelsVelocity = false;
    public Color parcelsVelocityColor = new Color(0f, 0f, 1f);
    public float parcelsVelocityPointRadius = 0.07f;
    public float parcelsVelocityArrowLengthFactor = 0.3f;
    public float parcelsVelocityArrowThickness = 0.04f;

    [Header("Velocity")]
    public Color velocityUColor = new Color(1f, 0f, 0f);
    public Color velocityVColor = new Color(0f, 1f, 0f);
    public float velocityPointRadius = 0.07f;
    public float velocityArrowLengthFactor = 0.3f;
    public float velocityArrowThickness = 0.04f;

    [Header("Velocity Map")]
    public float velocityDisplayRange = 1f;
    public Gradient velocityColorMap;

    [Header("Divergence")]
    public bool showDivergenceValue = false;
    public float divergenceDisplayRange = 1f;
    public Color negativeDivergenceColor = new Color(0.3f, 0.3f, 1f, 0.7f);
    public Color positiveDivergenceColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Header("Pressure")]
    public bool showPressureValue = false;
    public float pressureDisplayRange = 1f;
    public Color negativePressureColor = new Color(0.3f, 1f, 0.3f, 0.7f);
    public Color positivePressureColor = new Color(0.7f, 0.3f, 0.7f, 0.7f);

    [Header("Temperature")]
    public bool showTemperatureValue = false;
    public float temperatureDisplayRange = 1f;
    public Color coldTemperatureColor = new Color(0.3f, 0.3f, 1f, 0.7f);
    public Color hotTemperatureColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Header("Smoke")]
    public bool showSmokeValue = false;
    public float smokeDisplayRange = 1f;
    public Color smokeColor = new Color(1f, 1f, 1f, 0.6f);

    Vector2 CellDisplaySize => Vector2.one * CellSize * (1 - cellBorderThickness);
    public float HalfCellSize => CellSize * 0.5f;

    public void OnEnable() {
        isRenderingEnabled = true;
    }

    public void OnDisable() {
        isRenderingEnabled = false;
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
        return new Vector2(x - (grid.width - 1) * 0.5f, y - (grid.height - 1) * 0.5f) * CellSize;
    }

    /// <summary>
    /// Convert coordinates from the Uniy world reference system to the fluid
    /// Grid reference system.
    /// </summary>
    /// <param name="position">Coordinate in world space</param>
    /// <returns>Converted coordinates in Grid space</returns>
    public Vector2Int WorldToCellCenter(Vector2 position) {
        Vector2 cell = position / CellSize;
        cell.x += (grid.width - 1) * 0.5f;
        cell.y += (grid.height - 1) * 0.5f;
        return Vector2Int.RoundToInt(cell);
    }

    /// <summary>
    /// Set the fluid Grid to render
    /// </summary>
    /// <param name="grid">The Grid to render</param>
    public void SetGridToRender(FluidGridMac grid) {
        this.grid = grid;
    }

    /// <summary>
    /// Set the fluid Parcels to render
    /// </summary>
    /// <param name="parcels">The Parcels to render</param>
    public void SetParcelsToRender(FluidParcels parcels) {
        this.parcels = parcels;
    }

    /// <summary>
    /// Render everything based on the visualization mode
    /// </summary>
    /// <param name="mousePosition">Coordinates of the mouse</param>
    /// <param name="isMousePressed">Flag set to true if the left mouse button is pressed, false otherwise</param>
    public void Render(Vector2 mousePosition, bool isMousePressed) {
        if (!isRenderingEnabled)
            return;

        Draw.StartLayerIfNotInMatching(Vector2.zero, 1, false);

        RenderGrid();
        RenderParcels();
        if (visualizationMode == VisualizationMode.Velocity)
            RenderVelocities();

        Draw.Point(mousePosition, inputRadius, isMousePressed ? inputActiveColor : inputColor);

        Draw.StartLayerIfNotInMatching(Vector2.zero, 1, true);
        Draw.Text(FontType.JetbrainsMonoRegular, $"Mode: {visualizationMode}", 20f,
                  new Vector2(30f, Screen.height - 20f), Anchor.TopLeft, Color.white);
    }

    /// <summary>
    /// Render the fluid Grid
    /// </summary>
    void RenderGrid() {
        for (int i = 0; i < grid.width; ++i) {
            for (int j = 0; j < grid.height; ++j) {
                RenderCell(i, j);
            }
        }
    }

    /// <summary>
    /// Render the fluid Parcels
    /// </summary>
    void RenderParcels() {
        for (int i = 0; i < parcels.count; ++i) {
            Vector2 pos = parcels.position[i];
            Vector2 vel = parcels.velocity[i];
            Vector2 coords = CellCenterToWorld(pos.x, pos.y);
            Draw.Point(coords, Mathf.Sqrt(grid.width * grid.height) * 0.01f, Color.white);
            if (showParcelsVelocity) {
                RenderVelocityArrow(CellCenterToWorld(pos.x, pos.y), vel, parcelsVelocityColor, parcelsVelocityPointRadius,
                            parcelsVelocityArrowLengthFactor, parcelsVelocityArrowThickness);
            }
        }
    }

    /// <summary>
    /// Render a single Cell of the Grid
    /// </summary>
    /// <param name="i">Horizontal coordinate of the Cell in Grid space</param>
    /// <param name="j">Vertical coordinate of the Cell in Grid space</param>
    void RenderCell(int i, int j) {
        Color col = gridColor;
        Vector2 pos = CellCenterToWorld(i, j);

        switch (grid.cellType[i, j]) {
        case FluidGridMac.CellType.Solid:
            col = Color.orange;
            break;
        }

        switch (visualizationMode) {
        case VisualizationMode.VelocityMap:
            if (grid.cellType[i, j] == FluidGridMac.CellType.Solid)
                break;
            Vector2 velocity = grid.SampleVelocity(new Vector2(i, j));
            float velocityT = Mathf.Clamp(velocity.sqrMagnitude / velocityDisplayRange, 0, 1f);
            col = velocityColorMap.Evaluate(velocityT);
            break;
        case VisualizationMode.Divergence:
            float divergence = grid.CalculateDivergenceAtCell(i, j);
            float divergenceT = Mathf.Abs(divergence) / divergenceDisplayRange;
            col = Color.Lerp(col, divergence < 0 ? negativeDivergenceColor : positiveDivergenceColor, divergenceT);
            if (showDivergenceValue)
                Draw.Text(FontType.JetbrainsMonoRegular, $"{divergence:0.00}", fontSize, pos, Anchor.Centre,
                          Color.white);
            break;
        case VisualizationMode.Pressure:
            float pressure = grid.pressure[i, j];
            float pressureT = Mathf.Abs(pressure) / pressureDisplayRange;
            col = Color.Lerp(col, pressure < 0 ? negativePressureColor : positivePressureColor, pressureT);
            if (showPressureValue)
                Draw.Text(FontType.JetbrainsMonoRegular, $"{pressure:0.00}", fontSize, pos, Anchor.Centre, Color.white);
            break;
        case VisualizationMode.Temperature:
            if (grid.cellType[i, j] == FluidGridMac.CellType.Solid)
                break;
            float temperatureDegrees = grid.temperature[i, j] - 273.15f;
            float temperatureT = Mathf.Abs(temperatureDegrees) / temperatureDisplayRange;
            col = Color.Lerp(col, temperatureDegrees < 0 ? coldTemperatureColor : hotTemperatureColor, temperatureT);
            if (showTemperatureValue)
                Draw.Text(FontType.JetbrainsMonoRegular, $"{temperatureDegrees:0.00}", fontSize, pos, Anchor.Centre,
                          Color.white);
            break;
        case VisualizationMode.Smoke:
            if (grid.cellType[i, j] == FluidGridMac.CellType.Solid)
                break;
            float smoke = grid.smokeMap[i, j];
            float smokeT = Mathf.Clamp01(smoke / smokeDisplayRange);
            col = Color.Lerp(col, smokeColor, smokeT);
            if (showSmokeValue)
                Draw.Text(FontType.JetbrainsMonoRegular, $"{smoke:0.00}", fontSize, pos, Anchor.Centre, Color.white);
            break;
        }

        Draw.Quad(pos, CellDisplaySize, col);
    }

    /// <summary>
    /// Render all velocity arrows at the edges of the Grid Cells
    /// </summary>
    void RenderVelocities() {
        // Render horizontal arrows
        for (int i = 0; i <= grid.width; ++i) {
            for (int j = 0; j < grid.height; ++j) {
                float x = i - 0.5f;
                float y = j;
                float u = grid.GetVelocity(grid.velocityU, x, y, FluidGridMac.Axis.X);
                Vector2 pos = CellCenterToWorld(x, y);
                RenderVelocityArrow(pos, new Vector2(u, 0), velocityUColor, velocityPointRadius,
                                    velocityArrowLengthFactor, velocityArrowThickness);
            }
        }

        // Render vertical arrows
        for (int i = 0; i < grid.width; ++i) {
            for (int j = 0; j <= grid.height; ++j) {
                float x = i;
                float y = j - 0.5f;
                float v = grid.GetVelocity(grid.velocityV, x, y, FluidGridMac.Axis.Y);
                Vector2 pos = CellCenterToWorld(x, y);
                RenderVelocityArrow(pos, new Vector2(0, v), velocityVColor, velocityPointRadius,
                                    velocityArrowLengthFactor, velocityArrowThickness);
            }
        }
    }

    /// <summary>
    /// Render a single velocity arrow at a given position in world coordinates
    /// </summary>
    /// <param name="position">Position of the tail of the arrow in world space</param>
    /// <param name="velocity">Velocity value</param>
    /// <param name="color">Arrow color</param>
    /// <param name="pointRadius">Radius of the point located at the tail of the arrow</param>
    /// <param name="arrowLengthFactor">Multiplier used to change the arrow length</param>
    /// <param name="arrowThickness">Thickness of the arrow</param>
    void RenderVelocityArrow(Vector2 position, Vector2 velocity, Color color, float pointRadius,
                             float arrowLengthFactor, float arrowThickness) {
        Draw.Point(position, pointRadius, color);
        Draw.Arrow(position, position + velocity * arrowLengthFactor, arrowThickness, arrowThickness * 3.5f, 32, color);
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
