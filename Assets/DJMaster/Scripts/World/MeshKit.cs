using UnityEngine;

namespace DJMaster
{
    // Tiny procedural meshes, cheap enough to draw hundreds of on Quest.
    public static class MeshKit
    {
        // An n-sided prism (or truncated cone) standing on its base at the origin, growing along +Y.
        public static Mesh Prism(int sides, float radiusBottom, float radiusTop, float height, bool capped = true)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radiusBottom, 0, Mathf.Sin(a0) * radiusBottom);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radiusBottom, 0, Mathf.Sin(a1) * radiusBottom);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * radiusTop, height, Mathf.Sin(a0) * radiusTop);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * radiusTop, height, Mathf.Sin(a1) * radiusTop);
                int v = verts.Count;
                verts.AddRange(new[] { b0, b1, t1, t0 });
                tris.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
                if (capped)
                {
                    int c = verts.Count;
                    verts.AddRange(new[] { Vector3.zero, b0, b1, new Vector3(0, height, 0), t0, t1 });
                    tris.AddRange(new[] { c, c + 1, c + 2, c + 3, c + 5, c + 4 });
                }
            }
            var m = new Mesh { name = $"Prism{sides}" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // A unit-radius UV sphere (low poly), for heads, hands and hair.
        public static Mesh Sphere(int segments = 12, int rings = 8)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = (float)r / rings * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments * Mathf.PI * 2;
                    verts.Add(new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var m = new Mesh { name = "Sphere" };
            m.SetVertices(verts);
            m.SetNormals(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // Open cone for light beams: apex at the origin, opening along +Z.
        public static Mesh Beam(int sides, float length, float endRadius)
        {
            var verts = new Vector3[sides + 1];
            var uv = new Vector2[sides + 1];
            var tris = new int[sides * 6];
            verts[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * endRadius, Mathf.Sin(a) * endRadius, length);
                uv[i + 1] = new Vector2((float)i / sides, 1);
                int n = (i + 1) % sides + 1;
                // both faces so it shows from inside and out
                tris[i * 6] = 0; tris[i * 6 + 1] = i + 1; tris[i * 6 + 2] = n;
                tris[i * 6 + 3] = 0; tris[i * 6 + 4] = n; tris[i * 6 + 5] = i + 1;
            }
            var m = new Mesh { name = "Beam", vertices = verts, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }
    
        // Two crossed quads running along +Z from the origin, so a laser looks solid from any side.
        // UV: x across the beam, y along it.
        public static Mesh Laser(float length, float width)
        {
            float w = width * 0.5f;
            var verts = new[]
            {
                new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, 0, length), new Vector3(-w, 0, length),
                new Vector3(0, -w, 0), new Vector3(0, w, 0), new Vector3(0, w, length), new Vector3(0, -w, length),
            };
            var uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            };
            var tris = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            var m = new Mesh { name = "Laser", vertices = verts, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        // Many small camera-agnostic quads scattered over a sphere (or its upper half): stars, mirror-ball glints.
        public static Mesh Sparkles(int count, float radius, float size, bool upperHalfOnly, int seed)
        {
            var rng = new System.Random(seed);
            var verts = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var tris = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                Vector3 dir;
                do
                {
                    dir = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);
                } while (dir.sqrMagnitude > 1 || dir.sqrMagnitude < 0.01f || (upperHalfOnly && dir.y < 0.05f));
                dir.Normalize();
                Vector3 c = dir * radius;
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(right, dir);
                float s = size * (0.5f + (float)rng.NextDouble());
                int v = i * 4;
                verts[v] = c + (-right - up) * s; verts[v + 1] = c + (right - up) * s;
                verts[v + 2] = c + (right + up) * s; verts[v + 3] = c + (-right + up) * s;
                uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0); uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(0, 1);
                var col = Color.white * (0.4f + 0.6f * (float)rng.NextDouble());
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = col;
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            var m = new Mesh { name = "Sparkles", vertices = verts, uv = uv, colors = colors, triangles = tris };
            m.RecalculateBounds();
            return m;
        }
    }
}
