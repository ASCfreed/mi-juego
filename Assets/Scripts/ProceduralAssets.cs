using UnityEngine;

namespace ParticulasRA
{
    /// <summary>Mallas, texturas y materiales generados por código (sin assets externos).</summary>
    public static class ProceduralAssets
    {
        static Mesh sphere, quad, cube, pyramid;
        static Texture2D glow, dash, circle;
        static Sprite circleSprite;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public static Mesh Sphere => sphere ? sphere : sphere = BuildSphere(14, 9);
        public static Mesh Quad => quad ? quad : quad = BuildQuad();
        public static Mesh Cube => cube ? cube : cube = BuildCube();
        public static Mesh Pyramid => pyramid ? pyramid : pyramid = BuildPyramid();

        /// <summary>Degradado radial blanco para halos y destellos.</summary>
        public static Texture2D GlowTexture
        {
            get
            {
                if (glow) return glow;
                const int s = 64;
                glow = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Glow" };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
                glow.SetPixels32(px);
                glow.Apply(false, true);
                return glow;
            }
        }

        /// <summary>Textura discontinua (trazas de partículas neutras).</summary>
        public static Texture2D DashTexture
        {
            get
            {
                if (dash) return dash;
                dash = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Dash" };
                var px = new Color32[8];
                for (int i = 0; i < 8; i++) px[i] = new Color32(255, 255, 255, (byte)(i < 3 ? 255 : 0));
                dash.SetPixels32(px);
                dash.Apply(false, true);
                return dash;
            }
        }

        /// <summary>Círculo con borde suavizado para la interfaz.</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite) return circleSprite;
                const int s = 64;
                circle = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Circle" };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f;
                    float a = Mathf.Clamp01(s / 2f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                circle.SetPixels32(px);
                circle.Apply(false, true);
                circleSprite = Sprite.Create(circle, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
                return circleSprite;
            }
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, t);
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, t);
        }

        /// <summary>Copia un material base (creado como asset para que sus variantes entren en el build) y lo tiñe.</summary>
        public static Material Tinted(Material baseMat, Color c, Texture tex = null)
        {
            var m = new Material(baseMat) { enableInstancing = true };
            SetColor(m, c);
            if (tex != null) SetTexture(m, tex);
            return m;
        }

        // ---------- Mallas ----------
        static Mesh BuildSphere(int lon, int lat)
        {
            var verts = new Vector3[(lon + 1) * (lat + 1)];
            var tris = new int[lon * lat * 6];
            int v = 0;
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2f * Mathf.PI * x / lon;
                    verts[v++] = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b)) * 0.5f;
                }
            }
            int t = 0;
            for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int i = y * (lon + 1) + x;
                tris[t++] = i; tris[t++] = i + 1; tris[t++] = i + lon + 1;
                tris[t++] = i + 1; tris[t++] = i + lon + 2; tris[t++] = i + lon + 1;
            }
            var m = new Mesh { name = "EsferaProc", vertices = verts, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh
            {
                name = "QuadProc",
                vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildCube()
        {
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++)
                v[i] = new Vector3((i & 1) != 0 ? .5f : -.5f, (i & 2) != 0 ? .5f : -.5f, (i & 4) != 0 ? .5f : -.5f);
            var m = new Mesh
            {
                name = "CuboProc",
                vertices = v,
                triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,   4, 5, 6, 5, 7, 6,   // -Z, +Z
                    0, 1, 4, 1, 5, 4,   2, 6, 3, 3, 6, 7,   // -Y, +Y
                    0, 4, 2, 2, 4, 6,   1, 3, 5, 3, 7, 5,   // -X, +X
                },
            };
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildPyramid()
        {
            var m = new Mesh
            {
                name = "PiramideProc",
                vertices = new[]
                {
                    new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0),
                    new Vector3(0, 0, 1),
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 },
            };
            m.RecalculateBounds();
            return m;
        }
    }
}
