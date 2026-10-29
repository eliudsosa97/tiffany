using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    static class GroundLitterMeshes
    {
        public static Mesh[][] Load() => new[]
        {
            new[] { Generated("LitterLeaves0", geometry => Leaves(geometry, 0)), Generated("LitterLeaves1", geometry => Leaves(geometry, 1)),
                Generated("LitterLeaves2", geometry => Leaves(geometry, 2)) },
            new[] { Generated("LitterTwig0", geometry => Twig(geometry, 0)), Generated("LitterTwig1", geometry => Twig(geometry, 1)),
                Generated("LitterTwigPile", TwigPile) },
            new[] { Generated("LitterPineCone", PineCone) },
            new[] { Generated("LitterStone", geometry => Stone(geometry, Vector3.zero, 1f, 0f)), Generated("LitterGravel", Gravel) },
            new[] { Generated("LitterRoot0", geometry => Root(geometry, 0)), Generated("LitterRoot1", geometry => Root(geometry, 1)),
                Generated("LitterRoot2", geometry => Root(geometry, 2)) }
        };

        static Mesh Generated(string name, Action<Geometry> build)
        {
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            string path = $"{folder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            var geometry = new Geometry();
            build(geometry);
            if (isNew) mesh = new Mesh { name = name };
            else mesh.Clear();
            mesh.SetVertices(geometry.vertices);
            mesh.SetUVs(0, geometry.uv);
            mesh.SetTriangles(geometry.indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }

        static void Leaves(Geometry geometry, int variant)
        {
            int count = 28 + variant * 4;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f + variant * 0.47f;
                float radius = Mathf.Sqrt((i + 0.5f) / count) * (0.425f + variant * 0.09f);
                var center = new Vector3(Mathf.Cos(angle) * radius, 0.03f + i % 4 * 0.015f, Mathf.Sin(angle) * radius);
                var rotation = Quaternion.Euler(12f + i % 3 * 6f, i * 137.5f + variant * 31f, i % 5 * 6f - 12f);
                float width = 0.105f + i % 4 * 0.01f;
                float length = 0.135f + i % 3 * 0.015f;
                Vector3 Point(float x, float y, float z) => center + rotation * new Vector3(x, y, z);
                var tail = Point(0f, 0.016f, -length * 0.5f);
                var tip = Point(0f, 0.016f, length * 0.5f);
                geometry.Triangle(tail, Point(-width * 0.5f, 0f, 0f), tip, Vector2.zero, Vector2.up, Vector2.one);
                geometry.Triangle(tail, tip, Point(width * 0.5f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.right);
            }
        }

        static void Twig(Geometry geometry, int variant) =>
            Branch(geometry, Vector3.zero, 0f, variant == 0 ? 1.05f : 1.55f, variant == 1);

        static void Branch(Geometry geometry, Vector3 center, float yaw, float length, bool secondFork)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 Point(float x, float y, float z) => center + rotation * new Vector3(x, y, z);
            var start = Point(0f, 0.04f, -length * 0.5f);
            var fork = Point(0.04f, 0.07f, length * 0.08f);
            geometry.Rod(start, fork, 0.035f);
            geometry.Rod(fork, Point(-0.05f, 0.09f, length * 0.5f), 0.032f);
            geometry.Rod(fork, Point(length * 0.3f, 0.11f, length * 0.32f), 0.032f);
            if (secondFork) geometry.Rod(Vector3.Lerp(start, fork, 0.4f), Point(-length * 0.24f, 0.08f, -0.04f), 0.032f);
        }

        static void TwigPile(Geometry geometry)
        {
            for (int i = 0; i < 4; i++)
                Branch(geometry, new Vector3((i % 2 - 0.5f) * 0.18f, i * 0.035f, (i / 2 - 0.5f) * 0.16f),
                    -61f + i * 47f, 1.1f + i * 0.12f, false);
        }

        static void Stone(Geometry geometry, Vector3 center, float width, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 Rim(int i)
            {
                float angle = i * Mathf.PI / 3f;
                return center + rotation * new Vector3(Mathf.Cos(angle) * width * 0.5f, width * (0.22f + i % 2 * 0.04f),
                    Mathf.Sin(angle) * width * 0.5f);
            }
            for (int i = 0; i < 6; i++)
            {
                geometry.Triangle(Rim(i), center + Vector3.up * width * 0.52f, Rim(i + 1), Vector2.zero, Vector2.up, Vector2.one);
                geometry.Triangle(Rim(i), Rim(i + 1), center, Vector2.zero, Vector2.one, Vector2.right);
            }
        }

        static void Gravel(Geometry geometry)
        {
            for (int i = 0; i < 4; i++)
                Stone(geometry, new Vector3((i % 2 - 0.5f) * 0.32f, 0f, (i / 2 - 0.5f) * 0.30f),
                    0.11f + i * 0.023f, i * 47f);
        }

        static void Root(Geometry geometry, int variant)
        {
            Vector3 Point(int i)
            {
                float t = i / 6f;
                return new Vector3((t - 0.5f) * 1.6f, Mathf.Sin(t * Mathf.PI) * 0.23f - 0.06f,
                    Mathf.Sin(t * Mathf.PI * 2f + variant * 0.7f) * 0.22f);
            }
            for (int i = 0; i < 6; i++) geometry.Rod(Point(i), Point(i + 1), 0.05f, 1f);
        }

        static void PineCone(Geometry geometry)
        {
            for (int row = 0; row < 3; row++)
            for (int side = 0; side < 4; side++)
            {
                float angle = (side + (row % 2 == 0 ? 0.5f : 0f)) * Mathf.PI * 0.5f;
                var outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var tangent = new Vector3(-outward.y, outward.x, 0f);
                float radius = row == 1 ? 0.066f : row == 0 ? 0.048f : 0.04f;
                var axis = new Vector3(0f, 0.08f, (row - 1) * 0.045f);
                var center = axis + outward * radius;
                geometry.Fold(center - Vector3.forward * 0.035f,
                    axis + outward * (radius * 0.65f) + tangent * radius,
                    center + Vector3.forward * 0.035f,
                    axis + outward * (radius * 0.65f) - tangent * radius,
                    center + outward * 0.014f, Vector2.zero, Vector2.one);
            }
        }

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> indices = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(uvA); uv.Add(uvB); uv.Add(uvC);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }

            public void Fold(Vector3 tail, Vector3 left, Vector3 tip, Vector3 right, Vector3 ridge, Vector2 offset, Vector2 size)
            {
                var middle = offset + size * 0.5f;
                var bottom = offset + new Vector2(size.x * 0.5f, 0f);
                var west = offset + new Vector2(0f, size.y * 0.5f);
                var top = offset + new Vector2(size.x * 0.5f, size.y);
                var east = offset + new Vector2(size.x, size.y * 0.5f);
                Triangle(tail, left, ridge, bottom, west, middle);
                Triangle(left, tip, ridge, west, top, middle);
                Triangle(tip, right, ridge, top, east, middle);
                Triangle(right, tail, ridge, east, bottom, middle);
            }

            public void Rod(Vector3 start, Vector3 end, float radius, float taper = 0.8f)
            {
                var axis = (end - start).normalized;
                var across = Vector3.Cross(axis, Vector3.up).normalized;
                var up = Vector3.Cross(axis, across);
                var a = new Vector3[3];
                var b = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2f / 3f;
                    var offset = (across * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                    a[i] = start + offset;
                    b[i] = end + offset * taper;
                }
                for (int i = 0; i < 3; i++)
                {
                    int next = (i + 1) % 3;
                    Triangle(a[i], a[next], b[next], Vector2.zero, Vector2.right, Vector2.one);
                    Triangle(a[i], b[next], b[i], Vector2.zero, Vector2.one, Vector2.up);
                }
                Triangle(a[0], a[2], a[1], Vector2.zero, Vector2.up, Vector2.right);
                Triangle(b[0], b[1], b[2], Vector2.zero, Vector2.right, Vector2.up);
            }
        }
    }
}
