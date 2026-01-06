using UnityEngine;
using static UnityEngine.Mathf;
using System;

namespace FluidSimulation {
    public class FluidGrid {
        public enum CellType {
            Fluid,
            Solid,
        };

        struct PressureSolverData {
            public int flowTop;
            public int flowRight;
            public int flowBottom;
            public int flowLeft;
            public int flowEdgeCount;
            public bool isSolid;
            public float velocityTerm;
        };

        public FluidParcels parcels;

        public readonly int width;
        public readonly int height;
        public readonly float cellSize;

        public readonly Vector2 boundsSize;
        public readonly Vector2 bottomLeft;
        public readonly float halfCellSize;

        public readonly CellType[,] cellTypes;
        public readonly float[,] massX;
        public readonly float[,] massY;
        public readonly float[,] momentumX;
        public readonly float[,] momentumY;
        public readonly float[,] velocitiesX;
        public readonly float[,] velocitiesY;
        public readonly float[,] velocitiesXNext;
        public readonly float[,] velocitiesYNext;

        public readonly float[,] pressure;
        readonly PressureSolverData[,] pressureData;

        public float[,] smokeMap;
        public float[,] smokeMapTemp;

        const float density = 1f;
        float timeStep => 1f / 60f * timeStepMultiplier;
        public float timeStepMultiplier = 1;
        public float SOR = 1;

        public FluidGrid(int width, int height, float cellSize) {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;

            cellTypes = new CellType[width, height];
            massX = new float[width + 1, height];
            massY = new float[width, height + 1];
            momentumX = new float[width + 1, height];
            momentumY = new float[width, height + 1];
            velocitiesX = new float[width + 1, height];
            velocitiesY = new float[width, height + 1];
            velocitiesYNext = new float[width, height + 1];
            velocitiesXNext = new float[width + 1, height];
            pressureData = new PressureSolverData[width, height];
            pressure = new float[width, height];
            smokeMap = new float[width, height];
            smokeMapTemp = new float[width, height];

            // Treat border as solids
            for (int x = 0; x < width; ++x) {
                cellTypes[x, 0] = CellType.Solid;
                cellTypes[x, height - 1] = CellType.Solid;
            }
            for (int y = 0; y < height; ++y) {
                cellTypes[0, y] = CellType.Solid;
                cellTypes[width - 1, y] = CellType.Solid;
            }

            // Pre calculate useful values
            boundsSize = new Vector2(width, height) * cellSize;
            bottomLeft = -boundsSize * 0.5f;
            halfCellSize = cellSize * 0.5f;
        }

        public void PairParcels(FluidParcels parcels) {
            this.parcels = parcels;
        }

        public Vector2 CellCenter(int x, int y) => bottomLeft + new Vector2(x + 0.5f, y + 0.5f) * cellSize;
        public Vector2 LeftEdgeCenter(int x, int y) => CellCenter(x, y) - new Vector2(halfCellSize, 0);
        public Vector2 BottomEdgeCenter(int x, int y) => CellCenter(x, y) - new Vector2(0, halfCellSize);

        public Vector2Int CellCoordsFromPosition(Vector2 pos) {
            float x = (pos.x - bottomLeft.x) / cellSize - 0.5f;
            float y = (pos.y - bottomLeft.y) / cellSize - 0.5f;
            return new Vector2Int(RoundToInt(x), RoundToInt(y));
        }

        public bool IsSolid(int x, int y) {
            bool outOfBounds = x < 0 || x >= width || y < 0 || y >= height;
            return outOfBounds ? true : (cellTypes[x, y] == CellType.Solid);
        }

        public float GetPressure(int x, int y) {
            bool outOfBounds = x < 0 || x >= width || y < 0 || y >= height;
            return outOfBounds ? 0f : pressure[x, y];
        }

        public static float SampleBilinear(float[,] edgeValues, float cellSize, Vector2 pos) {
            int edgeCountX = edgeValues.GetLength(0);
            int edgeCountY = edgeValues.GetLength(1);
            float w = (edgeCountX - 1) * cellSize;
            float h = (edgeCountY - 1) * cellSize;

            // Calculate indices of each edge for the current cell
            float x = (pos.x + w * 0.5f) / cellSize;
            float y = (pos.y + h * 0.5f) / cellSize;

            int left = Clamp((int)x, 0, edgeCountX - 2);
            int bottom = Clamp((int)y, 0, edgeCountY - 2);
            int right = left + 1;
            int top = bottom + 1;

            // Calculate how far [0,1] the input point is along the current cell
            float xFrac = Clamp01(x - left);
            float yFrac = Clamp01(y - bottom);

            // Bilinear interpolation
            float interpTop = Lerp(edgeValues[left, top], edgeValues[right, top], xFrac);
            float interpBottom = Lerp(edgeValues[left, bottom], edgeValues[right, bottom], xFrac);
            return Lerp(interpBottom, interpTop, yFrac);
        }

        public Vector2 SampleVelocity(Vector2 pos) {
            float vx = SampleBilinear(velocitiesX, cellSize, pos);
            float vy = SampleBilinear(velocitiesY, cellSize, pos);
            return new Vector2(vx, vy);
        }

        public float CalculateDivergenceAtCell(int x, int y) {
            float velocityTop = velocitiesY[x, y + 1];
            float velocityRight = velocitiesX[x + 1, y];
            float velocityBottom = velocitiesY[x, y];
            float velocityLeft = velocitiesX[x, y];

            float gradientX = (velocityRight - velocityLeft) / cellSize;
            float gradientY = (velocityTop - velocityBottom) / cellSize;
            float gradient = gradientX + gradientY;
            return gradient;
        }

        public void Reset() {
            Array.Clear(massX, 0, massX.Length);
            Array.Clear(massY, 0, massY.Length);
            Array.Clear(momentumX, 0, momentumX.Length);
            Array.Clear(momentumY, 0, momentumY.Length);
        }

        void TransferMassBilinear(float[,] mass, Vector2 pos, float parcelMass) {
            float x = (pos.x - bottomLeft.x) / cellSize;
            float y = (pos.y - bottomLeft.y) / cellSize;

            Vector2Int cell = CellCoordsFromPosition(pos);
            int left = Clamp(cell.x, 0, width - 2);
            int bottom = Clamp(cell.y, 0, height - 2);
            int right = left + 1;
            int top = bottom + 1;

            // Calculate how far [0,1] the input point is along the current cell
            float xFrac = Clamp01(x - left);
            float yFrac = Clamp01(y - bottom);

            // Distribute mass between adjacent cells
            mass[left, bottom] += parcelMass * (1f - xFrac) * (1f - yFrac);
            mass[right, bottom] += parcelMass * (xFrac) * (1f - yFrac);
            mass[left, top] += parcelMass * (1f - xFrac) * (yFrac);
            mass[right, top] += parcelMass * (xFrac) * (yFrac);
        }

        public void TransferMass() {
            for (int i = 0; i < parcels.count; ++i) {
                Vector2 pos = parcels.position[i];
                float m = parcels.mass[i];
                TransferMassBilinear(massX, pos - Vector2.up * 0.5f, m);
                TransferMassBilinear(massY, pos - Vector2.right * 0.5f, m);
            }
        }

        public void SolvePressure(int iterations) {
            PreparePressureSolver();

            for (int i = 0; i < iterations; ++i) {
                RunPressureSolver();
            }
        }

        void PreparePressureSolver() {
            for (int x = 0; x < width; ++x) {
                for (int y = 0; y < height; ++y) {
                    int flowTop = IsSolid(x, y + 1) ? 0 : 1;
                    int flowRight = IsSolid(x + 1, y) ? 0 : 1;
                    int flowBottom = IsSolid(x, y - 1) ? 0 : 1;
                    int flowLeft = IsSolid(x - 1, y) ? 0 : 1;
                    int flowEdgeCount = flowTop + flowRight + flowBottom + flowLeft;
                    bool isSolid = IsSolid(x, y);

                    float velocityTop = velocitiesY[x, y + 1];
                    float velocityRight = velocitiesX[x + 1, y];
                    float velocityBottom = velocitiesY[x, y];
                    float velocityLeft = velocitiesX[x, y];
                    float velocityTerm = (velocityTop - velocityBottom + velocityRight - velocityLeft) / timeStep;

                    pressureData[x, y] = new PressureSolverData() {
                        flowTop = flowTop,
                        flowRight = flowRight,
                        flowBottom = flowBottom,
                        flowLeft = flowLeft,
                        flowEdgeCount = flowEdgeCount,
                        isSolid = isSolid,
                        velocityTerm = velocityTerm,
                    };
                }
            }
        }

        void RunPressureSolver() {
            for (int x = 0; x < width; ++x) {
                for (int y = 0; y < height; ++y) {
                    float newPressure = 0;
                    PressureSolverData info = pressureData[x, y];

                    if (!info.isSolid && info.flowEdgeCount != 0) {
                        float pressureTop = GetPressure(x, y + 1) * info.flowTop;
                        float pressureRight = GetPressure(x + 1, y) * info.flowRight;
                        float pressureBottom = GetPressure(x, y - 1) * info.flowBottom;
                        float pressureLeft = GetPressure(x - 1, y) * info.flowLeft;

                        float pressureSum = pressureTop + pressureRight + pressureBottom + pressureLeft;
                        newPressure = (pressureSum - density * cellSize * info.velocityTerm) / (float)info.flowEdgeCount;
                    }

                    float oldPressure = pressure[x, y];
                    pressure[x, y] = oldPressure + (newPressure - oldPressure) * SOR;
                }
            }
        }

        public void UpdateVelocities() {
            float k = timeStep / (density * cellSize);

            // Horizontal
            for (int x = 0; x < velocitiesX.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesX.GetLength(1); ++y) {
                    if (IsSolid(x, y) || IsSolid(x - 1, y)) {
                        velocitiesX[x, y] = 0f;
                        continue;
                    }

                    float pressureRight = GetPressure(x, y);
                    float pressureLeft = GetPressure(x - 1, y);
                    velocitiesX[x, y] -= k * (pressureRight - pressureLeft);
                }
            }

            // Vertical
            for (int x = 0; x < velocitiesY.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesY.GetLength(1); ++y) {
                    if (IsSolid(x, y) || IsSolid(x, y - 1)) {
                        velocitiesY[x, y] = 0f;
                        continue;
                    }

                    float pressureTop = GetPressure(x, y);
                    float pressureBottom = GetPressure(x, y - 1);
                    velocitiesY[x, y] -= k * (pressureTop - pressureBottom);
                }
            }
        }

        public void AdvectVelocity() {
            for (int x = 0; x < velocitiesX.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesX.GetLength(1); ++y) {
                    if (IsSolid(x - 1, y) || IsSolid(x, y)) {
                        velocitiesXNext[x, y] = velocitiesX[x, y];
                        continue;
                    }

                    Vector2 position = LeftEdgeCenter(x, y);
                    Vector2 velocity = SampleVelocity(position);
                    Vector2 positionPrev = position - velocity * timeStep;
                    velocitiesXNext[x, y] = SampleVelocity(positionPrev).x;
                }
            }

            for (int x = 0; x < velocitiesY.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesY.GetLength(1); ++y) {
                    if (IsSolid(x - 1, y) || IsSolid(x, y)) {
                        velocitiesYNext[x, y] = velocitiesY[x, y];
                        continue;
                    }

                    Vector2 position = BottomEdgeCenter(x, y);
                    Vector2 velocity = SampleVelocity(position);
                    Vector2 positionPrev = position - velocity * timeStep;
                    velocitiesYNext[x, y] = SampleVelocity(positionPrev).y;
                }
            }

            // Update velocities
            for (int x = 0; x < velocitiesX.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesX.GetLength(1); ++y) {
                    velocitiesX[x, y] = velocitiesXNext[x, y];
                }
            }

            for (int x = 0; x < velocitiesY.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesY.GetLength(1); ++y) {
                    velocitiesY[x, y] = velocitiesYNext[x, y];
                }
            }
        }

        public void ClearVelocities() {
            for (int x = 0; x < velocitiesX.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesX.GetLength(1); ++y) {
                    velocitiesX[x, y] = 0;
                }
            }

            for (int x = 0; x < velocitiesY.GetLength(0); ++x) {
                for (int y = 0; y < velocitiesY.GetLength(1); ++y) {
                    velocitiesY[x, y] = 0;
                }
            }

            for (int x = 0; x < width; ++x) {
                for (int y = 0; y < height; ++y) {
                    pressure[x, y] = 0;
                }
            }
        }

        public void AdvectSmoke() {
            for (int x = 0; x < smokeMap.GetLength(0); ++x) {
                for (int y = 0; y < smokeMap.GetLength(1); ++y) {
                    Vector2 position = CellCenter(x, y);
                    Vector2 velocity = SampleVelocity(position);
                    Vector2 positionPrev = position - velocity * timeStep;
                    smokeMapTemp[x, y] = SampleBilinear(smokeMap, cellSize, positionPrev);
                }
            }

            for (int x = 0; x < smokeMap.GetLength(0); ++x) {
                for (int y = 0; y < smokeMap.GetLength(1); ++y) {
                    smokeMap[x, y] = smokeMapTemp[x, y];
                }
            }
        }

        public void AddSmokeAtPosition(Vector2 pos, float amount, float radius) {
            Vector2Int centerCell = CellCoordsFromPosition(pos);
            int radiusInCells = Mathf.CeilToInt(radius / cellSize);

            for (int dx = -radiusInCells; dx <= radiusInCells; ++dx) {
                for (int dy = -radiusInCells; dy <= radiusInCells; ++dy) {
                    int x = centerCell.x + dx;
                    int y = centerCell.y + dy;

                    if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1 || cellTypes[x, y] == CellType.Solid) {
                        continue;
                    }

                    Vector2 cellCenter = CellCenter(x, y);
                    float dist = Vector2.Distance(pos, cellCenter);
                    if (dist <= radius) {
                        float falloff = 1f - (dist / radius);
                        smokeMap[x, y] += amount * falloff;
                    }
                }
            }
        }

        public void ClearSmoke() {
            for (int x = 0; x < smokeMap.GetLength(0); ++x) {
                for (int y = 0; y < smokeMap.GetLength(1); ++y) {
                    smokeMap[x, y] = 0f;
                    smokeMapTemp[x, y] = 0f;
                }
            }
        }
        public void AddShapeAtPosition(Vector2 position, float radius) {
            Vector2Int centerCoord = CellCoordsFromPosition(position);
            int numCellsHalf = CeilToInt(radius / cellSize * 0.5f);
            for (int offx = -numCellsHalf; offx <= numCellsHalf; ++offx) {
                for (int offy = -numCellsHalf; offy <= numCellsHalf; ++offy) {
                    int x = centerCoord.x + offx;
                    int y = centerCoord.y + offy;
                    if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
                        continue;
                    cellTypes[x, y] = CellType.Solid;
                }
            }
        }

        public void AddCircleAtPosition(Vector2 position, float radius) {
            Vector2Int centerCoord = CellCoordsFromPosition(position);
            int numCellsHalf = CeilToInt(radius / cellSize * 0.5f);
            for (int offx = -numCellsHalf; offx <= numCellsHalf; ++offx) {
                for (int offy = -numCellsHalf; offy <= numCellsHalf; ++offy) {
                    int x = centerCoord.x + offx;
                    int y = centerCoord.y + offy;
                    if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
                        continue;
                    float dist = Vector2.Distance(new Vector2(x, y), centerCoord);
                    if (dist <= radius) {
                        cellTypes[x, y] = CellType.Solid;
                    }
                }
            }
        }

        public void ClearShapes() {
            for (int x = 0; x < width; ++x) {
                for (int y = 0; y < height; ++y) {
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                        continue;
                    cellTypes[x, y] = CellType.Fluid;
                }
            }
        }
    }
}