using UnityEngine;
using Seb.Helpers;
using Seb.Vis;
using static UnityEngine.Mathf;

namespace FluidSimulation {
    public class FluidDrawer : MonoBehaviour {
        public enum VisualizationMode {
            None,
            Divergence,
            Pressure,
            Velocity,
        };

		public VisualizationMode visualizationMode;
        public float fontSize;

        [Header("Interaction")]
        public float interactionRadius;
        public float interactionStrength;

		[Header("Cell")]
		public float cellBorderThickness;
        public Color cellColor;

        [Header("Velocity")]
		public Color velocityXColor;
		public Color velocityYColor;
        public float velocityPointRadius;
        public float velocityArrowLengthFactor;
        public float velocityArrowThickness;

        [Header("Interpolated Velocity")]
        public int interpolatedGridResolution;
		public Color interpolatedVelocityColor;
        public float interpolatedVelocityPointRadius;
        public float interpolatedVelocityArrowLengthFactor;
        public float interpolatedVelocityArrowThickness;
        
        [Header("Divergence")]
        public float divergenceDisplayRange;
        public Color negativeDivergenceColor;
        public Color positiveDivergenceColor;
        
        [Header("Pressure")]
        public float pressureDisplayRange;
        public Color negativePressureColor;
        public Color positivePressureColor;

        FluidGrid fluidGrid;
		Vector2 cellDisplaySize;

        bool isInteracting;
        Vector2 mousePositionOld;

        public void SetFluidGridToVisualize(FluidGrid grid) {
            fluidGrid = grid;
            cellDisplaySize = Vector2.one * grid.cellSize * (1 - cellBorderThickness);
        }

        public void Visualize() {
			Draw.StartLayerIfNotInMatching(Vector2.zero, 1, false);

            // Draw grid
            for (int x = 0; x < fluidGrid.width; ++x) {
                for (int y = 0; y < fluidGrid.height; ++y) {
                    DrawCell(x, y);
                }
            }

            if (visualizationMode == VisualizationMode.Velocity) {
                int interpolatedWidth = fluidGrid.width * interpolatedGridResolution + 1;
                int interpolatedHeight = fluidGrid.height * interpolatedGridResolution + 1;

                for (int px = 0; px < interpolatedWidth; ++px) {
                    for (int py = 0; py < interpolatedHeight; ++py) {
                        float tx = px / (interpolatedWidth - 1f);
                        float ty = py / (interpolatedHeight - 1f);
                        Vector2 position = fluidGrid.bottomLeft + new Vector2(tx * fluidGrid.boundsSize.x, ty * fluidGrid.boundsSize.y);
                        Vector2 velocity = fluidGrid.SampleVelocity(position);
                        DrawVelocityArrow(
                            position,
                            velocity,
                            interpolatedVelocityColor,
                            interpolatedVelocityPointRadius,
                            interpolatedVelocityArrowLengthFactor,
                            interpolatedVelocityArrowThickness);
                    }
                }
            }

            // Draw horizontal velocities
            for (int x = 0; x < fluidGrid.velocitiesX.GetLength(0); ++x) {
                for (int y = 0; y < fluidGrid.velocitiesX.GetLength(1); ++y) {
                    DrawVelocityArrow(
                        fluidGrid.LeftEdgeCenter(x, y),
                        Vector2.right * fluidGrid.velocitiesX[x, y],
                        velocityXColor,
                        velocityPointRadius,
                        velocityArrowLengthFactor,
                        velocityArrowThickness);
                }
            }

            // Draw vertical velocities
            for (int x = 0; x < fluidGrid.velocitiesY.GetLength(0); ++x) {
                for (int y = 0; y < fluidGrid.velocitiesY.GetLength(1); ++y) {
                    DrawVelocityArrow(
                        fluidGrid.BottomEdgeCenter(x, y),
                        Vector2.up * fluidGrid.velocitiesY[x, y],
                        velocityYColor,
                        velocityPointRadius,
                        velocityArrowLengthFactor,
                        velocityArrowThickness);
                }
            }
        }

        void DrawCell(int x, int y) {
            Color col = cellColor;

            switch (fluidGrid.cellTypes[x, y]) {
                case FluidGrid.CellType.Solid:
                    col = Color.gray;
                    break;

                default:
                    break;
            }

            switch (visualizationMode) {
                case VisualizationMode.Divergence:
                    float divergence = fluidGrid.CalculateDivergenceAtCell(x, y);
                    float divergenceT = Abs(divergence) / divergenceDisplayRange;
                    col = Color.Lerp(col, divergence < 0 ? negativeDivergenceColor : positiveDivergenceColor, divergenceT);
                    Draw.Text(FontType.JetbrainsMonoRegular, $"{divergence:0.00}", fontSize, fluidGrid.CellCenter(x, y), Anchor.Centre, Color.white);
                    break;
                case VisualizationMode.Pressure:
                    float pressure = fluidGrid.pressure[x, y];
                    float pressureT = Abs(pressure) / pressureDisplayRange;
                    col = Color.Lerp(col, pressure < 0 ? negativePressureColor : positivePressureColor, pressureT);
                    Draw.Text(FontType.JetbrainsMonoRegular, $"{pressure:0.00}", fontSize, fluidGrid.CellCenter(x, y), Anchor.Centre, Color.white);
                    break;
                default:
                    break;
            }

            Draw.Quad(fluidGrid.CellCenter(x, y), cellDisplaySize, col);
        }

        void DrawVelocityArrow(
            Vector2 pos,
            Vector2 velocity,
            Color col,
            float pointRadius,
            float arrowLengthFactor,
            float arrowThickness)
        {
            Draw.Point(pos, pointRadius, col);
            Draw.Arrow(pos, pos + velocity * arrowLengthFactor, arrowThickness, arrowThickness * 3.5f, 32, col);
        }

        public void HandleInteraction() {
            Vector2 mousePosition = InputHelper.MousePosWorld;
            isInteracting = InputHelper.IsMouseHeld(MouseButton.Left);

			if (InputHelper.IsKeyDownThisFrame(KeyCode.Tab)) {
                var modeCount = VisualizationMode.GetNames(typeof(VisualizationMode)).Length;
                int direction = InputHelper.ShiftIsHeld ? -1 : 1;

                int mode = (int)visualizationMode + direction;
                if (mode < 0)
                    mode += modeCount;
                mode %= modeCount;

                visualizationMode = (VisualizationMode)mode;
            }

            if (isInteracting) {
                Vector2Int centerCoord = fluidGrid.CellCoordsFromPosition(mousePosition);

                Vector2 mouseDelta = mousePosition - mousePositionOld;
                int numCellsHalf = CeilToInt(interactionRadius / fluidGrid.cellSize * 0.5f);
                for (int offy = -numCellsHalf; offy <= numCellsHalf; ++offy) {
                    for (int offx = -numCellsHalf; offx <= numCellsHalf; ++offx) {
                        int x = centerCoord.x + offx;
                        int y = centerCoord.y + offy;
                        if (x < 0 || x >= fluidGrid.width || y < 0 || y >= fluidGrid.height)
                            continue;

                        Vector2 cellPosition = fluidGrid.CellCenter(x, y);
                        float weight = 1 - Maths.Clamp01((cellPosition - mousePosition).sqrMagnitude / (interactionRadius * interactionRadius));
                        fluidGrid.velocitiesX[x, y] += mouseDelta.x * weight * interactionStrength;
                        fluidGrid.velocitiesY[x, y] += mouseDelta.y * weight * interactionStrength;
                    }
                }

            }

			mousePositionOld = mousePosition;
        }
    }
}
