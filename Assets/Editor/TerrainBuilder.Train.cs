using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class TerrainBuilder
    {
        static void BuildTrainBridge(Transform parent, RouteTerrain terrain, Material structure, Material gravel)
        {
            Vector3 tracks = RouteLayout.Clearing(2);
            float south = tracks.z + RouteLayout.TrainSouthRampLength;
            float north = RouteLayout.TrainNorthEnd - RouteLayout.TrainRampLength;
            Deck("South Ramp", tracks.z, south);
            Deck("Deck", south, north);
            Deck("North Ramp", north, RouteLayout.TrainNorthEnd);
            for (float z = tracks.z + 9f; z < RouteLayout.TrainNorthEnd; z += 9f)
            {
                float bottom = terrain.GroundHeight(tracks.x, z);
                float top = terrain.TrainHeight(z) - 0.2f;
                if (top <= bottom) continue;
                BlockNavigation(SceneBuilder.CreatePrimitive("Pier", PrimitiveType.Cube, parent,
                    new Vector3(tracks.x, (bottom + top) * 0.5f, z), new Vector3(0.6f, top - bottom, 0.6f), structure));
            }
            void Deck(string name, float start, float end)
            {
                int steps = Mathf.CeilToInt((end - start) / GridSize);
                var vertices = new Vector3[(steps + 1) * 2];
                var triangles = new List<int>();
                var section = Root(name, parent);
                for (int i = 0; i <= steps; i++)
                {
                    float z = Mathf.Lerp(start, end, (float)i / steps);
                    Vector3 point = new Vector3(tracks.x, terrain.TrainHeight(z) + FloorOffset, z);
                    vertices[i * 2] = point + Vector3.left * (RouteLayout.TrainWidth * 0.5f);
                    vertices[i * 2 + 1] = point + Vector3.right * (RouteLayout.TrainWidth * 0.5f);
                    if (i == 0) continue;
                    int a = (i - 1) * 2;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    Vector3 previous = (vertices[a] + vertices[a + 1]) * 0.5f;
                    Vector3 middle = (previous + point) * 0.5f;
                    Quaternion rotation = Quaternion.LookRotation(point - previous);
                    float length = Vector3.Distance(previous, point);
                    var beam = Part("Deck Beam", middle - Vector3.up * 0.12f,
                        new Vector3(RouteLayout.TrainWidth, 0.24f, length + 0.02f), structure);
                    Object.DestroyImmediate(beam.GetComponent<Collider>());
                    var area = beam.AddComponent<NavMeshModifierVolume>();
                    area.area = 0;
                    area.center = Vector3.up * 0.5f;
                    area.size = new Vector3(1f, 3f, 1.02f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var track = Part("Rail", middle + new Vector3(side * 0.9f, 0.04f, 0f),
                            new Vector3(0.12f, 0.08f, length + 0.02f), gravel);
                        track.AddComponent<GroundSurface>().type = GroundType.Gravel;
                        track.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                        var guard = Part("Guardrail", middle + new Vector3(side * (RouteLayout.TrainWidth * 0.5f + 0.1f), 0.6f, 0f),
                            new Vector3(0.15f, 1.2f, length + 0.03f), structure);
                        BlockNavigation(guard);
                    }
                    GameObject Part(string partName, Vector3 position, Vector3 size, Material material)
                    {
                        var piece = SceneBuilder.CreatePrimitive(partName, PrimitiveType.Cube, section, position, size, material);
                        piece.transform.localRotation = rotation;
                        return piece;
                    }
                }
                var mesh = SaveMesh("Train " + name, vertices, triangles.ToArray());
                var floor = MeshObject("Ground", section, mesh, gravel);
                floor.AddComponent<MeshCollider>().sharedMesh = mesh;
                floor.AddComponent<GroundSurface>().type = GroundType.Gravel;
            }
        }
    }
}
