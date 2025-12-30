using UnityEngine;
using Seb.Helpers;
using Seb.Vis;
using static UnityEngine.Mathf;

namespace FluidSimulation {
    public class FluidDrawer : MonoBehaviour {
        public enum VisualizationMode {
            None,
            Mass,
            Divergence,
            Pressure,
            Velocity,
            VelocityUI,
            Smoke,
        };

        public VisualizationMode visualizationMode;
        public float fontSize;

        [Header("Interaction")]
        public float interactionRadius;
        public float interactionStrength;
        public Color interactionColor;
        public Color interactionActiveColor;

        [Header("Parcel")]
        public float parcelSize;
        public Color parcelColor;

        [Header("Cell")]
        public float cellBorderThickness;
        public Color cellColor;

        [Header("Mass")]
        public bool showMassValue;
        public float massDisplayRange;
        public Color massXColor;
        public Color massYColor;

        [Header("Velocity")]
        public bool showVelocityArrows;
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
        public bool showDivergenceValue;
        public float divergenceDisplayRange;
        public Color negativeDivergenceColor;
        public Color positiveDivergenceColor;

        [Header("Pressure")]
        public bool showPressureValue;
        public float pressureDisplayRange;
        public Color negativePressureColor;
        public Color positivePressureColor;

        [Header("Velocity-UI")]
        public float maxVelocityVisualized = 5f;
        public Gradient velocityColorMap;
        public bool showCenterVelocityArrows;
        FluidParcels fluidParcels;
        FluidGrid fluidGrid;
        Vector2 cellDisplaySize;

        [Header("Smoke")]
        public bool showSmokeValue;
        public float smokeDisplayRange;
        public Color smokeColor;
        public float smokeAmountPerInteraction;
        public float smokeSpawnRadius;

        bool isInteracting;
        Vector2 mousePositionOld;

        public void SetParcelsToVisualize(FluidParcels parcels) {
            fluidParcels = parcels;
        }

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

            if (visualizationMode == VisualizationMode.Mass) {
                // Draw horizontal masses
                for (int x = 0; x < fluidGrid.massX.GetLength(0); ++x) {
                    for (int y = 0; y < fluidGrid.massX.GetLength(1); ++y) {
                        float mass = fluidGrid.massX[x, y];
                        float massT = Abs(mass) / massDisplayRange;
                        Vector2 pos = fluidGrid.LeftEdgeCenter(x, y);

                        Color col = massXColor;
                        col.a *= massT;

                        Draw.Diamond(
                            pos,
                            Vector2.one * fluidGrid.cellSize,
                            col
                        );

                        if (showMassValue) {
                            Draw.Text(
                                FontType.JetbrainsMonoRegular,
                                $"{mass:0.00}",
                                fontSize,
                                pos,
                                Anchor.Centre,
                                Color.white
                            );
                        }
                    }
                }

                // Draw vertical masses
                for (int x = 0; x < fluidGrid.massY.GetLength(0); ++x) {
                    for (int y = 0; y < fluidGrid.massY.GetLength(1); ++y) {
                        float mass = fluidGrid.massY[x, y];
                        float massT = Abs(mass) / massDisplayRange;
                        Vector2 pos = fluidGrid.BottomEdgeCenter(x, y);

                        Color col = massYColor;
                        col.a *= massT;

                        Draw.Diamond(
                            pos,
                            Vector2.one * fluidGrid.cellSize,
                            col
                        );

                        if (showMassValue) {
                            Draw.Text(
                                FontType.JetbrainsMonoRegular,
                                $"{mass:0.00}",
                                fontSize,
                                pos,
                                Anchor.Centre,
                                Color.white
                            );
                        }
                    }
                }
            }

            // Draw parcels
            for (int i = 0; i < fluidParcels.count; ++i) {
                DrawParcel(i);
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

            if (visualizationMode == VisualizationMode.VelocityUI && showCenterVelocityArrows) {
                for (int x = 0; x < fluidGrid.width; ++x) {
                    for (int y = 0; y < fluidGrid.height; ++y) {
                        Vector2 position = fluidGrid.CellCenter(x, y);
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

            if (showVelocityArrows) {
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

            // Draw mouse overlay
            Draw.Point(mousePositionOld, interactionRadius, isInteracting ? interactionActiveColor : interactionColor);
        }

        void DrawParcel(int i) {
            Color col = parcelColor;
            Vector2 pos = fluidParcels.position[i];
            Draw.Point(pos, parcelSize, col);
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
                    if (showDivergenceValue) {
                        Draw.Text(FontType.JetbrainsMonoRegular, $"{divergence:0.00}", fontSize, fluidGrid.CellCenter(x, y), Anchor.Centre, Color.white);
                    }
                    break;
                case VisualizationMode.Pressure:
                    float pressure = fluidGrid.pressure[x, y];
                    float pressureT = Abs(pressure) / pressureDisplayRange;
                    col = Color.Lerp(col, pressure < 0 ? negativePressureColor : positivePressureColor, pressureT);
                    if (showPressureValue) {
                        Draw.Text(FontType.JetbrainsMonoRegular, $"{pressure:0.00}", fontSize, fluidGrid.CellCenter(x, y), Anchor.Centre, Color.white);
                    }
                    break;
                case VisualizationMode.VelocityUI:
                    if (fluidGrid.cellTypes[x, y] == FluidGrid.CellType.Solid) {
                        break;
                    }
                    Vector2 position = fluidGrid.CellCenter(x, y);
                    Vector2 velocity = fluidGrid.SampleVelocity(position);
                    float speedT = velocity.magnitude / maxVelocityVisualized;
                    col = velocityColorMap.Evaluate(speedT);
                    break;
                case VisualizationMode.Smoke:
                    float smokeAmount = fluidGrid.smokeMap[x, y];
                    float smokeT = Mathf.Clamp01(smokeAmount / smokeDisplayRange);
                    col = Color.Lerp(col, smokeColor, smokeT);
                    if (showSmokeValue) {
                        Draw.Text(FontType.JetbrainsMonoRegular, $"{smokeAmount:0.00}", fontSize, fluidGrid.CellCenter(x, y), Anchor.Centre, Color.white);
                    }
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
            float arrowThickness) {
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

            if (InputHelper.IsMouseHeld(MouseButton.Right)) {
                fluidGrid.AddSmokeAtPosition(mousePosition, smokeAmountPerInteraction, smokeSpawnRadius);
            }

            mousePositionOld = mousePosition;
        }
    }
}
