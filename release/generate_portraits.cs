using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BrokenCharacters.Tools
{
    public class V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator *(V3 a, float s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public static V3 operator /(V3 a, float s) { return new V3(a.X / s, a.Y / s, a.Z / s); }
        public float Dot(V3 b) { return X * b.X + Y * b.Y + Z * b.Z; }
        public V3 Cross(V3 b) { return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X); }
        public float Length() { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
        public V3 Normalized()
        {
            float l = Length();
            return l > 1e-6f ? this / l : new V3(0, 1, 0);
        }
    }

    public class Tri
    {
        public V3 P0, P1, P2;
        public V3 N0, N1, N2;
        public Color C0, C1, C2;
        public float Emit;
    }

    public class ZRasterizer
    {
        public int Width, Height;
        public float[] ZBuffer;
        public Color[] FrameBuffer;

        public ZRasterizer(int w, int h)
        {
            Width = w;
            Height = h;
            ZBuffer = new float[w * h];
            FrameBuffer = new Color[w * h];
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < ZBuffer.Length; i++)
            {
                ZBuffer[i] = float.MaxValue;
                FrameBuffer[i] = Color.FromArgb(0, 0, 0, 0);
            }
        }

        public void DrawMesh(List<Tri> tris, V3 camPos, V3 target, float fovDeg, V3 lightDir, Color ambient)
        {
            V3 fwd = (target - camPos).Normalized();
            V3 upRef = new V3(0, 1, 0);
            V3 right = fwd.Cross(upRef).Normalized();
            V3 up = right.Cross(fwd).Normalized();

            float aspect = (float)Width / Height;
            float halfFov = (float)(fovDeg * Math.PI / 360.0);
            float tanHalfFov = (float)Math.Tan(halfFov);

            lightDir = lightDir.Normalized();

            foreach (Tri t in tris)
            {
                V3 rel0 = t.P0 - camPos;
                V3 rel1 = t.P1 - camPos;
                V3 rel2 = t.P2 - camPos;

                float z0 = rel0.Dot(fwd);
                float z1 = rel1.Dot(fwd);
                float z2 = rel2.Dot(fwd);

                if (z0 <= 0.05f || z1 <= 0.05f || z2 <= 0.05f) continue;

                V3 fn = (t.P1 - t.P0).Cross(t.P2 - t.P0).Normalized();
                if (fn.Dot(rel0) >= 0f) continue;

                float x0 = (rel0.Dot(right) / (z0 * tanHalfFov * aspect) + 1f) * 0.5f * Width;
                float y0 = (1f - (rel0.Dot(up) / (z0 * tanHalfFov) + 1f) * 0.5f) * Height;

                float x1 = (rel1.Dot(right) / (z1 * tanHalfFov * aspect) + 1f) * 0.5f * Width;
                float y1 = (1f - (rel1.Dot(up) / (z1 * tanHalfFov) + 1f) * 0.5f) * Height;

                float x2 = (rel2.Dot(right) / (z2 * tanHalfFov * aspect) + 1f) * 0.5f * Width;
                float y2 = (1f - (rel2.Dot(up) / (z2 * tanHalfFov) + 1f) * 0.5f) * Height;

                Color col0 = Shade(t.C0, t.N0, lightDir, fwd, t.Emit, ambient);
                Color col1 = Shade(t.C1, t.N1, lightDir, fwd, t.Emit, ambient);
                Color col2 = Shade(t.C2, t.N2, lightDir, fwd, t.Emit, ambient);

                RasterizeTriangle(x0, y0, z0, col0,
                                  x1, y1, z1, col1,
                                  x2, y2, z2, col2);
            }
        }

        private Color Shade(Color baseCol, V3 nrm, V3 lightDir, V3 viewFwd, float emit, Color ambient)
        {
            if (emit > 0.05f)
            {
                float boost = 1.0f + emit * 0.35f;
                int r = Math.Min(255, (int)(baseCol.R * boost));
                int g = Math.Min(255, (int)(baseCol.G * boost));
                int b = Math.Min(255, (int)(baseCol.B * boost));
                return Color.FromArgb(255, r, g, b);
            }

            float ndotl = Math.Max(0f, nrm.Dot(lightDir));
            float cel = ndotl > 0.18f ? 1.0f : 0.64f;
            float rim = (float)Math.Pow(Math.Max(0f, 1f + nrm.Dot(viewFwd)), 2.8) * 0.30f;

            float rF = (baseCol.R / 255f) * (ambient.R / 255f + cel * 0.70f) + rim;
            float gF = (baseCol.G / 255f) * (ambient.G / 255f + cel * 0.70f) + rim;
            float bF = (baseCol.B / 255f) * (ambient.B / 255f + cel * 0.70f) + rim;

            return Color.FromArgb(255,
                Math.Min(255, Math.Max(0, (int)(rF * 255f))),
                Math.Min(255, Math.Max(0, (int)(gF * 255f))),
                Math.Min(255, Math.Max(0, (int)(bF * 255f))));
        }

        private void RasterizeTriangle(float x0, float y0, float z0, Color c0,
                                       float x1, float y1, float z1, Color c1,
                                       float x2, float y2, float z2, Color c2)
        {
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxX = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxY = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));

            float denom = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
            if (Math.Abs(denom) < 1e-6f) return;
            float invDenom = 1f / denom;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                int rowOffset = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;

                    float w0 = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) * invDenom;
                    float w1 = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) * invDenom;
                    float w2 = 1f - w0 - w1;

                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                    {
                        float z = w0 * z0 + w1 * z1 + w2 * z2;
                        int idx = rowOffset + x;
                        if (z < ZBuffer[idx])
                        {
                            ZBuffer[idx] = z;
                            int r = (int)(w0 * c0.R + w1 * c1.R + w2 * c2.R);
                            int g = (int)(w0 * c0.G + w1 * c1.G + w2 * c2.G);
                            int b = (int)(w0 * c0.B + w1 * c1.B + w2 * c2.B);
                            FrameBuffer[idx] = Color.FromArgb(255, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
                        }
                    }
                }
            }
        }

        public Bitmap ToBitmap()
        {
            Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] bytes = new byte[Width * Height * 4];
            for (int i = 0; i < FrameBuffer.Length; i++)
            {
                Color c = FrameBuffer[i];
                bytes[i * 4 + 0] = c.B;
                bytes[i * 4 + 1] = c.G;
                bytes[i * 4 + 2] = c.R;
                bytes[i * 4 + 3] = c.A;
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            bmp.UnlockBits(data);
            return bmp;
        }
    }

    public class ModelLoader
    {
        public static List<Tri> LoadDuelModel(string path, HashSet<string> allowedBones)
        {
            List<Tri> list = new List<Tri>();
            Stream fs = File.OpenRead(path);
            Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? (Stream)new GZipStream(fs, CompressionMode.Decompress)
                : fs;

            using (BinaryReader br = new BinaryReader(stream))
            {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != "DUEL") throw new Exception("Not a DUEL file: " + path);
                int ver = br.ReadInt32();
                int nb = br.ReadInt32();
                for (int i = 0; i < nb; i++)
                {
                    int l = br.ReadInt32();
                    br.ReadBytes(l);
                    br.ReadBytes(24);
                }
                int np = br.ReadInt32();
                Color[] pal = new Color[np];
                float[] emit = new float[np];
                for (int i = 0; i < np; i++)
                {
                    float r = br.ReadSingle();
                    float g = br.ReadSingle();
                    float b = br.ReadSingle();
                    float em = br.ReadSingle();
                    pal[i] = Color.FromArgb(255, Math.Min(255, (int)(r * 255f)), Math.Min(255, (int)(g * 255f)), Math.Min(255, (int)(b * 255f)));
                    emit[i] = em;
                }
                int nparts = br.ReadInt32();
                for (int i = 0; i < nparts; i++)
                {
                    int l = br.ReadInt32();
                    string bname = Encoding.UTF8.GetString(br.ReadBytes(l));
                    int vc = br.ReadInt32();

                    bool keep = allowedBones == null || allowedBones.Contains(bname);
                    for (int j = 0; j < vc; j += 3)
                    {
                        V3 p0 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        V3 n0 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        int pal0 = br.ReadInt32();

                        V3 p1 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        V3 n1 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        int pal1 = br.ReadInt32();

                        V3 p2 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        V3 n2 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        int pal2 = br.ReadInt32();

                        if (keep)
                        {
                            Tri t = new Tri();
                            t.P0 = p0; t.P1 = p1; t.P2 = p2;
                            t.N0 = n0; t.N1 = n1; t.N2 = n2;
                            t.C0 = pal[pal0]; t.C1 = pal[pal1]; t.C2 = pal[pal2];
                            t.Emit = Math.Max(emit[pal0], Math.Max(emit[pal1], emit[pal2]));
                            list.Add(t);
                        }
                    }
                }
            }
            return list;
        }

        public static List<Tri> LoadWilburModel(string path)
        {
            List<Tri> list = new List<Tri>();
            Color pink = Color.FromArgb(255, 246, 178, 185);
            Color snoutPink = Color.FromArgb(255, 235, 145, 155);
            Color darkEye = Color.FromArgb(255, 30, 24, 28);
            Color earInner = Color.FromArgb(255, 255, 195, 202);

            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader br = new BinaryReader(fs))
            {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != "WPIG") throw new Exception("Not a WPIG file");
                int ver = br.ReadInt32();
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int l = br.ReadInt32();
                    string name = Encoding.UTF8.GetString(br.ReadBytes(l));
                    int frame = br.ReadInt32();
                    int nr = br.ReadInt32();
                    for (int k = 0; k < nr; k++)
                    {
                        int rl = br.ReadInt32();
                        br.ReadBytes(rl);
                    }
                    int vc = br.ReadInt32();
                    V3[] v = new V3[vc];
                    for (int k = 0; k < vc; k++) v[k] = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    V3[] nrArr = new V3[vc];
                    for (int k = 0; k < vc; k++) nrArr[k] = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    int ic = br.ReadInt32();
                    int[] idx = new int[ic];
                    for (int k = 0; k < ic; k++) idx[k] = br.ReadInt32();

                    if (name.Contains("Leg") || name.Contains("Hoof") || name.Contains("Tail")) continue;

                    // Place parts into root space:
                    V3 offset = new V3(0, 0, 0);
                    if (name == "Pig_Head" || name == "Pig_Tuft0")
                        offset = new V3(0, 0.84f, 0.50f);
                    else if (name == "Pig_Snout" || name == "Pig_Jowls")
                        offset = new V3(0, 0.84f - 0.05f, 0.50f + 0.38f);
                    else if (name.Contains("EarL"))
                        offset = new V3(-0.22f, 0.84f + 0.20f, 0.50f + 0.08f);
                    else if (name.Contains("EarR"))
                        offset = new V3(0.22f, 0.84f + 0.20f, 0.50f + 0.08f);

                    Color col = pink;
                    if (name.Contains("Snout")) col = snoutPink;
                    else if (name.Contains("EarInner")) col = earInner;
                    else if (name.Contains("Spot")) col = darkEye;

                    for (int k = 0; k < ic; k += 3)
                    {
                        int i0 = idx[k], i1 = idx[k + 1], i2 = idx[k + 2];
                        Tri t = new Tri();
                        t.P0 = v[i0] + offset; t.P1 = v[i1] + offset; t.P2 = v[i2] + offset;
                        t.N0 = nrArr[i0]; t.N1 = nrArr[i1]; t.N2 = nrArr[i2];
                        t.C0 = col; t.C1 = col; t.C2 = col;
                        t.Emit = 0f;
                        list.Add(t);
                    }
                }
            }
            return list;
        }
    }

    public class MeshBuilder
    {
        public static List<Tri> BuildClayRebel()
        {
            List<Tri> list = new List<Tri>();
            Color clayBlue = Color.FromArgb(255, 36, 75, 125);
            Color darkTar = Color.FromArgb(255, 28, 26, 30);
            Color bronze = Color.FromArgb(255, 140, 105, 60);
            Color cyanEye = Color.FromArgb(255, 40, 230, 255);
            Color gunMetal = Color.FromArgb(255, 60, 64, 70);

            // Clay pot helmet
            AddCylinder(list, new V3(0, 1.48f, 0), 0.26f, 0.30f, 0.36f, clayBlue, 18);
            AddTorus(list, new V3(0, 1.64f, 0), 0.30f, 0.05f, bronze, 16);
            AddBox(list, new V3(0.04f, 1.50f, 0.28f), new V3(0.24f, 0.045f, 0.06f), cyanEye, 2.5f);
            AddHorn(list, new V3(-0.24f, 1.58f, -0.04f), new V3(-0.52f, 1.88f, -0.18f), 0.08f, 0.02f, bronze, 10);
            AddHorn(list, new V3(0.24f, 1.58f, -0.04f), new V3(0.52f, 1.88f, -0.18f), 0.08f, 0.02f, bronze, 10);

            // Layered Clay chest
            AddBox(list, new V3(0, 1.15f, 0), new V3(0.60f, 0.40f, 0.38f), clayBlue, 0f);
            AddBox(list, new V3(0, 1.32f, 0), new V3(0.44f, 0.12f, 0.32f), darkTar, 0f);

            // Heavy layered pauldrons
            AddBox(list, new V3(-0.42f, 1.25f, 0.02f), new V3(0.28f, 0.22f, 0.32f), bronze, 0f);
            AddBox(list, new V3(0.42f, 1.25f, 0.02f), new V3(0.28f, 0.22f, 0.32f), bronze, 0f);

            // Minigun pointing 3/4 right
            V3 gunPos = new V3(0.38f, 1.08f, 0.28f);
            AddCylinder(list, gunPos, 0.12f, 0.12f, 0.48f, gunMetal, 12);
            AddCylinder(list, gunPos + new V3(0, 0, 0.26f), 0.14f, 0.14f, 0.08f, bronze, 12);
            for (int k = 0; k < 6; k++)
            {
                float ang = (float)(k * Math.PI / 3.0);
                V3 bPos = gunPos + new V3((float)Math.Cos(ang) * 0.08f, (float)Math.Sin(ang) * 0.08f, 0.32f);
                AddCylinder(list, bPos, 0.025f, 0.025f, 0.32f, gunMetal, 8);
            }

            return list;
        }

        public static List<Tri> BuildGravitos()
        {
            List<Tri> list = new List<Tri>();
            Color suitDark = Color.FromArgb(255, 65, 68, 78);
            Color suitViolet = Color.FromArgb(255, 145, 75, 220);
            Color visorDome = Color.FromArgb(255, 30, 24, 52);
            Color hudCyan = Color.FromArgb(255, 0, 245, 255);
            Color corePurple = Color.FromArgb(255, 220, 85, 255);
            Color podMetal = Color.FromArgb(255, 125, 130, 145);

            // Torso (layered Engineer armored chassis)
            AddBox(list, new V3(0, 1.15f, 0), new V3(0.64f, 0.42f, 0.40f), suitDark, 0f);
            AddBox(list, new V3(0, 1.18f, 0.14f), new V3(0.46f, 0.32f, 0.20f), suitViolet, 0f);
            AddBox(list, new V3(0, 0.98f, 0.08f), new V3(0.50f, 0.12f, 0.30f), suitDark, 0f);

            // Singularity Core Reactor in chest
            AddSphere(list, new V3(0.04f, 1.18f, 0.24f), 0.10f, corePurple, 3.0f, 16);
            AddTorus(list, new V3(0.04f, 1.18f, 0.24f), 0.14f, 0.022f, hudCyan, 16, 2.0f);
            AddTorus(list, new V3(0.04f, 1.18f, 0.24f), 0.18f, 0.015f, suitViolet, 14, 0.5f);

            // Dome helmet (Engineer EVA dome)
            AddSphere(list, new V3(0, 1.54f, 0), 0.27f, visorDome, 0f, 18);
            AddTorus(list, new V3(0.04f, 1.54f, 0.12f), 0.20f, 0.028f, hudCyan, 16, 2.5f);
            AddBox(list, new V3(0.04f, 1.44f, 0.22f), new V3(0.18f, 0.07f, 0.08f), podMetal, 0f);

            // Dual massive shoulder graviton cannons
            V3 podL = new V3(-0.46f, 1.44f, -0.05f);
            V3 podR = new V3(0.46f, 1.44f, -0.05f);
            AddCylinder(list, podL, 0.11f, 0.13f, 0.38f, podMetal, 12);
            AddCylinder(list, podR, 0.11f, 0.13f, 0.38f, podMetal, 12);
            AddSphere(list, podL + new V3(0, 0, 0.22f), 0.09f, corePurple, 2.8f, 12);
            AddSphere(list, podR + new V3(0, 0, 0.22f), 0.09f, corePurple, 2.8f, 12);

            // Biceps / Arms
            AddBox(list, new V3(-0.44f, 1.12f, 0), new V3(0.20f, 0.30f, 0.22f), suitViolet, 0f);
            AddBox(list, new V3(0.44f, 1.12f, 0), new V3(0.20f, 0.30f, 0.22f), suitViolet, 0f);

            return list;
        }

        public static List<Tri> BuildEmberwarden()
        {
            List<Tri> list = new List<Tri>();
            Color solarOrange = Color.FromArgb(255, 255, 120, 18);
            Color magmaDark = Color.FromArgb(255, 55, 34, 32);
            Color goldVisor = Color.FromArgb(255, 255, 215, 40);
            Color moltenGlow = Color.FromArgb(255, 255, 95, 10);
            Color gunMetal = Color.FromArgb(255, 82, 85, 92);
            Color flameCrest = Color.FromArgb(255, 255, 160, 25);

            // Hornet Helmet & Molten Visor
            AddBox(list, new V3(0, 1.54f, 0), new V3(0.38f, 0.34f, 0.38f), magmaDark, 0f);
            AddBox(list, new V3(0.04f, 1.54f, 0.20f), new V3(0.30f, 0.08f, 0.06f), goldVisor, 3.2f);
            AddBox(list, new V3(0.04f, 1.63f, 0.16f), new V3(0.24f, 0.06f, 0.12f), flameCrest, 1.5f);
            AddBox(list, new V3(0.04f, 1.45f, 0.18f), new V3(0.20f, 0.08f, 0.08f), gunMetal, 0f);

            // Torso (Heavy Magma Armor)
            AddBox(list, new V3(0, 1.16f, 0), new V3(0.65f, 0.44f, 0.38f), solarOrange, 0f);
            AddBox(list, new V3(0.02f, 1.16f, 0.18f), new V3(0.42f, 0.28f, 0.08f), magmaDark, 0f);
            // Three glowing horizontal chest thermal vents
            AddBox(list, new V3(0.04f, 1.24f, 0.22f), new V3(0.26f, 0.04f, 0.04f), moltenGlow, 3.0f);
            AddBox(list, new V3(0.04f, 1.16f, 0.22f), new V3(0.26f, 0.04f, 0.04f), moltenGlow, 3.0f);
            AddBox(list, new V3(0.04f, 1.08f, 0.22f), new V3(0.26f, 0.04f, 0.04f), moltenGlow, 3.0f);

            // Heavy angular flame pauldrons on shoulders
            AddBox(list, new V3(-0.46f, 1.32f, 0.02f), new V3(0.28f, 0.24f, 0.34f), solarOrange, 0f);
            AddBox(list, new V3(0.46f, 1.32f, 0.02f), new V3(0.28f, 0.24f, 0.34f), solarOrange, 0f);
            AddCylinder(list, new V3(-0.46f, 1.44f, -0.06f), 0.055f, 0.055f, 0.16f, gunMetal, 8);
            AddCylinder(list, new V3(0.46f, 1.44f, -0.06f), 0.055f, 0.055f, 0.16f, gunMetal, 8);

            // Biceps / Gauntlets
            AddBox(list, new V3(-0.46f, 1.08f, 0.02f), new V3(0.22f, 0.26f, 0.22f), magmaDark, 0f);
            AddBox(list, new V3(0.46f, 1.08f, 0.02f), new V3(0.22f, 0.26f, 0.22f), magmaDark, 0f);

            return list;
        }

        private static void AddBox(List<Tri> list, V3 c, V3 sz, Color col, float emit)
        {
            float hx = sz.X * 0.5f, hy = sz.Y * 0.5f, hz = sz.Z * 0.5f;
            V3[] v = new V3[]
            {
                c + new V3(-hx, -hy, -hz), c + new V3(hx, -hy, -hz), c + new V3(hx, hy, -hz), c + new V3(-hx, hy, -hz),
                c + new V3(-hx, -hy, hz),  c + new V3(hx, -hy, hz),  c + new V3(hx, hy, hz),  c + new V3(-hx, hy, hz)
            };
            AddQuad(list, v[4], v[5], v[6], v[7], new V3(0, 0, 1), col, emit);
            AddQuad(list, v[1], v[0], v[3], v[2], new V3(0, 0, -1), col, emit);
            AddQuad(list, v[0], v[4], v[7], v[3], new V3(-1, 0, 0), col, emit);
            AddQuad(list, v[5], v[1], v[2], v[6], new V3(1, 0, 0), col, emit);
            AddQuad(list, v[7], v[6], v[2], v[3], new V3(0, 1, 0), col, emit);
            AddQuad(list, v[0], v[1], v[5], v[4], new V3(0, -1, 0), col, emit);
        }

        private static void AddQuad(List<Tri> list, V3 p0, V3 p1, V3 p2, V3 p3, V3 n, Color c, float emit)
        {
            Tri t0 = new Tri { P0 = p0, P1 = p1, P2 = p2, N0 = n, N1 = n, N2 = n, C0 = c, C1 = c, C2 = c, Emit = emit };
            Tri t1 = new Tri { P0 = p0, P1 = p2, P2 = p3, N0 = n, N1 = n, N2 = n, C0 = c, C1 = c, C2 = c, Emit = emit };
            list.Add(t0); list.Add(t1);
        }

        private static void AddCylinder(List<Tri> list, V3 c, float r0, float r1, float h, Color col, int segs)
        {
            float hh = h * 0.5f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = (float)(i * Math.PI * 2.0 / segs);
                float a1 = (float)((i + 1) * Math.PI * 2.0 / segs);

                V3 p0 = c + new V3((float)Math.Cos(a0) * r0, -hh, (float)Math.Sin(a0) * r0);
                V3 p1 = c + new V3((float)Math.Cos(a1) * r0, -hh, (float)Math.Sin(a1) * r0);
                V3 p2 = c + new V3((float)Math.Cos(a1) * r1, hh, (float)Math.Sin(a1) * r1);
                V3 p3 = c + new V3((float)Math.Cos(a0) * r1, hh, (float)Math.Sin(a0) * r1);

                V3 n0 = new V3((float)Math.Cos(a0), 0, (float)Math.Sin(a0));
                V3 n1 = new V3((float)Math.Cos(a1), 0, (float)Math.Sin(a1));

                Tri t0 = new Tri { P0 = p0, P1 = p1, P2 = p2, N0 = n0, N1 = n1, N2 = n1, C0 = col, C1 = col, C2 = col };
                Tri t1 = new Tri { P0 = p0, P1 = p2, P2 = p3, N0 = n0, N1 = n1, N2 = n0, C0 = col, C1 = col, C2 = col };
                list.Add(t0); list.Add(t1);
            }
        }

        private static void AddHorn(List<Tri> list, V3 root, V3 tip, float rBase, float rTip, Color col, int segs)
        {
            V3 dir = tip - root;
            float len = dir.Length();
            V3 mid = (root + tip) * 0.5f + new V3(0, 0.06f, -0.05f);
            AddCylinder(list, (root + mid) * 0.5f, rBase, (rBase + rTip) * 0.5f, len * 0.5f, col, segs);
            AddCylinder(list, (mid + tip) * 0.5f, (rBase + rTip) * 0.5f, rTip, len * 0.5f, col, segs);
        }

        private static void AddSphere(List<Tri> list, V3 c, float r, Color col, float emit, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float lat0 = (float)(Math.PI * (-0.5 + (double)i / n));
                float z0 = (float)Math.Sin(lat0);
                float zr0 = (float)Math.Cos(lat0);

                float lat1 = (float)(Math.PI * (-0.5 + (double)(i + 1) / n));
                float z1 = (float)Math.Sin(lat1);
                float zr1 = (float)Math.Cos(lat1);

                for (int j = 0; j < 2 * n; j++)
                {
                    float lng0 = (float)(2.0 * Math.PI * (double)j / (2 * n));
                    float x0 = (float)Math.Cos(lng0), y0 = (float)Math.Sin(lng0);
                    float lng1 = (float)(2.0 * Math.PI * (double)(j + 1) / (2 * n));
                    float x1 = (float)Math.Cos(lng1), y1 = (float)Math.Sin(lng1);

                    V3 p0 = c + new V3(x0 * zr0, z0, y0 * zr0) * r;
                    V3 p1 = c + new V3(x1 * zr0, z0, y1 * zr0) * r;
                    V3 p2 = c + new V3(x1 * zr1, z1, y1 * zr1) * r;
                    V3 p3 = c + new V3(x0 * zr1, z1, y0 * zr1) * r;

                    V3 n0 = (p0 - c).Normalized();
                    V3 n1 = (p1 - c).Normalized();
                    V3 n2 = (p2 - c).Normalized();
                    V3 n3 = (p3 - c).Normalized();

                    Tri t0 = new Tri { P0 = p0, P1 = p1, P2 = p2, N0 = n0, N1 = n1, N2 = n2, C0 = col, C1 = col, C2 = col, Emit = emit };
                    Tri t1 = new Tri { P0 = p0, P1 = p2, P2 = p3, N0 = n0, N1 = n2, N2 = n3, C0 = col, C1 = col, C2 = col, Emit = emit };
                    list.Add(t0); list.Add(t1);
                }
            }
        }

        private static void AddTorus(List<Tri> list, V3 c, float R, float r, Color col, int segs, float emit = 0f)
        {
            for (int i = 0; i < segs; i++)
            {
                float u0 = (float)(i * Math.PI * 2.0 / segs);
                float u1 = (float)((i + 1) * Math.PI * 2.0 / segs);
                for (int j = 0; j < segs; j++)
                {
                    float v0 = (float)(j * Math.PI * 2.0 / segs);
                    float v1 = (float)((j + 1) * Math.PI * 2.0 / segs);

                    V3 p0 = c + TorusPt(R, r, u0, v0);
                    V3 p1 = c + TorusPt(R, r, u1, v0);
                    V3 p2 = c + TorusPt(R, r, u1, v1);
                    V3 p3 = c + TorusPt(R, r, u0, v1);

                    V3 center0 = c + new V3((float)Math.Cos(u0) * R, 0, (float)Math.Sin(u0) * R);
                    V3 n0 = (p0 - center0).Normalized();
                    V3 n1 = (p1 - center0).Normalized();

                    Tri t0 = new Tri { P0 = p0, P1 = p1, P2 = p2, N0 = n0, N1 = n1, N2 = n1, C0 = col, C1 = col, C2 = col, Emit = emit };
                    Tri t1 = new Tri { P0 = p0, P1 = p2, P2 = p3, N0 = n0, N1 = n1, N2 = n0, C0 = col, C1 = col, C2 = col, Emit = emit };
                    list.Add(t0); list.Add(t1);
                }
            }
        }

        private static V3 TorusPt(float R, float r, float u, float v)
        {
            float x = (float)((R + r * Math.Cos(v)) * Math.Cos(u));
            float y = (float)(r * Math.Sin(v));
            float z = (float)((R + r * Math.Cos(v)) * Math.Sin(u));
            return new V3(x, y, z);
        }
    }

    public class RoR2PortraitProcessor
    {
        public static Bitmap SegmentBackground(Bitmap src, Color bgSample, float tolerance, bool flipHoriz, float zoom, float panX, float panY)
        {
            int w = src.Width, h = src.Height;
            bool[,] isBg = new bool[w, h];
            Queue<Point> q = new Queue<Point>();

            for (int x = 0; x < w; x++)
            {
                CheckAndPush(src, x, 0, bgSample, tolerance, isBg, q);
                CheckAndPush(src, x, h - 1, bgSample, tolerance, isBg, q);
            }
            for (int y = 0; y < h; y++)
            {
                CheckAndPush(src, 0, y, bgSample, tolerance, isBg, q);
                CheckAndPush(src, w - 1, y, bgSample, tolerance, isBg, q);
            }

            int[] dx = new int[] { 1, -1, 0, 0 };
            int[] dy = new int[] { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                Point pt = q.Dequeue();
                Color cCur = src.GetPixel(pt.X, pt.Y);

                for (int i = 0; i < 4; i++)
                {
                    int nx = pt.X + dx[i];
                    int ny = pt.Y + dy[i];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !isBg[nx, ny])
                    {
                        Color cN = src.GetPixel(nx, ny);
                        float dr = (cN.R - bgSample.R) / 255f;
                        float dg = (cN.G - bgSample.G) / 255f;
                        float db = (cN.B - bgSample.B) / 255f;
                        float distKey = (float)Math.Sqrt(dr * dr + dg * dg + db * db);

                        float ldr = (cN.R - cCur.R) / 255f;
                        float ldg = (cN.G - cCur.G) / 255f;
                        float ldb = (cN.B - cCur.B) / 255f;
                        float distLocal = (float)Math.Sqrt(ldr * ldr + ldg * ldg + ldb * ldb);

                        if (distKey < tolerance || (distKey < tolerance * 1.5f && distLocal < 0.05f))
                        {
                            isBg[nx, ny] = true;
                            q.Enqueue(new Point(nx, ny));
                        }
                    }
                }
            }

            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (isBg[x, y])
                    {
                        result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    }
                    else
                    {
                        Color c = src.GetPixel(x, y);
                        result.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }
            }

            if (flipHoriz)
            {
                result.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }

            if (Math.Abs(zoom - 1.0f) > 0.01f || Math.Abs(panX) > 0.01f || Math.Abs(panY) > 0.01f)
            {
                Bitmap zoomed = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(zoomed))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float nw = w * zoom;
                    float nh = h * zoom;
                    float nx = (w - nw) * 0.5f + panX * w;
                    float ny = (h - nh) * 0.5f + panY * h;
                    g.DrawImage(result, nx, ny, nw, nh);
                }
                result.Dispose();
                result = zoomed;
            }

            return result;
        }

        private static void CheckAndPush(Bitmap src, int x, int y, Color bgSample, float tol, bool[,] isBg, Queue<Point> q)
        {
            if (isBg[x, y]) return;
            Color c = src.GetPixel(x, y);
            float dr = (c.R - bgSample.R) / 255f;
            float dg = (c.G - bgSample.G) / 255f;
            float db = (c.B - bgSample.B) / 255f;
            float dist = (float)Math.Sqrt(dr * dr + dg * dg + db * db);
            if (dist < tol * 1.5f)
            {
                isBg[x, y] = true;
                q.Enqueue(new Point(x, y));
            }
        }

        // Dedicated chroma-key segmentation for vignettes
        public static Bitmap SegmentChroma(Bitmap src, Func<Color, bool> isBackground, bool flipHoriz, float zoom, float panX, float panY)
        {
            int w = src.Width, h = src.Height;
            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = src.GetPixel(x, y);
                    if (isBackground(c))
                    {
                        result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    }
                    else
                    {
                        result.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }
            }

            if (flipHoriz)
            {
                result.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }

            if (Math.Abs(zoom - 1.0f) > 0.01f || Math.Abs(panX) > 0.01f || Math.Abs(panY) > 0.01f)
            {
                Bitmap zoomed = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(zoomed))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float nw = w * zoom;
                    float nh = h * zoom;
                    float nx = (w - nw) * 0.5f + panX * w;
                    float ny = (h - nh) * 0.5f + panY * h;
                    g.DrawImage(result, nx, ny, nw, nh);
                }
                result.Dispose();
                result = zoomed;
            }

            return result;
        }

        public static Bitmap AddRoR2Outline(Bitmap src, float outlineRadius, bool addRoR2Background)
        {
            int w = src.Width, h = src.Height;
            Bitmap output = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            byte[] srcBytes = new byte[w * h * 4];
            BitmapData srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length);
            src.UnlockBits(srcData);

            bool[] isOpaque = new bool[w * h];
            for (int i = 0; i < isOpaque.Length; i++)
            {
                isOpaque[i] = srcBytes[i * 4 + 3] > 30;
            }

            float[] dist = new float[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = isOpaque[i] ? 0f : 9999f;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    float d = dist[idx];
                    if (x > 0) d = Math.Min(d, dist[idx - 1] + 1f);
                    if (y > 0)
                    {
                        d = Math.Min(d, dist[idx - w] + 1f);
                        if (x > 0) d = Math.Min(d, dist[idx - w - 1] + 1.414f);
                        if (x < w - 1) d = Math.Min(d, dist[idx - w + 1] + 1.414f);
                    }
                    dist[idx] = d;
                }
            }

            for (int y = h - 1; y >= 0; y--)
            {
                int row = y * w;
                for (int x = w - 1; x >= 0; x--)
                {
                    int idx = row + x;
                    float d = dist[idx];
                    if (x < w - 1) d = Math.Min(d, dist[idx + 1] + 1f);
                    if (y < h - 1)
                    {
                        d = Math.Min(d, dist[idx + w] + 1f);
                        if (x > 0) d = Math.Min(d, dist[idx + w - 1] + 1.414f);
                        if (x < w - 1) d = Math.Min(d, dist[idx + w + 1] + 1.414f);
                    }
                    dist[idx] = d;
                }
            }

            byte[] outBytes = new byte[w * h * 4];
            Color whiteOutline = Color.FromArgb(255, 252, 252, 252);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float ny = (float)y / h - 0.5f;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    int bIdx = idx * 4;

                    float nx = (float)x / w - 0.5f;
                    float radial = (float)Math.Sqrt(nx * nx + ny * ny) * 1.414f;

                    int bgR = (int)(181 - radial * 70);
                    int bgG = (int)(159 - radial * 64);
                    int bgB = (int)(114 - radial * 48);
                    bgR = Math.Max(0, Math.Min(255, bgR));
                    bgG = Math.Max(0, Math.Min(255, bgG));
                    bgB = Math.Max(0, Math.Min(255, bgB));

                    byte srcA = srcBytes[bIdx + 3];
                    float d = dist[idx];

                    if (srcA > 20)
                    {
                        outBytes[bIdx + 0] = srcBytes[bIdx + 0];
                        outBytes[bIdx + 1] = srcBytes[bIdx + 1];
                        outBytes[bIdx + 2] = srcBytes[bIdx + 2];
                        outBytes[bIdx + 3] = 255;
                    }
                    else if (d <= outlineRadius)
                    {
                        float edgeAlpha = Math.Max(0f, Math.Min(1f, (outlineRadius - d) + 0.6f));
                        if (addRoR2Background)
                        {
                            outBytes[bIdx + 0] = (byte)(whiteOutline.B * edgeAlpha + bgB * (1f - edgeAlpha));
                            outBytes[bIdx + 1] = (byte)(whiteOutline.G * edgeAlpha + bgG * (1f - edgeAlpha));
                            outBytes[bIdx + 2] = (byte)(whiteOutline.R * edgeAlpha + bgR * (1f - edgeAlpha));
                            outBytes[bIdx + 3] = 255;
                        }
                        else
                        {
                            outBytes[bIdx + 0] = whiteOutline.B;
                            outBytes[bIdx + 1] = whiteOutline.G;
                            outBytes[bIdx + 2] = whiteOutline.R;
                            outBytes[bIdx + 3] = (byte)(edgeAlpha * 255);
                        }
                    }
                    else
                    {
                        if (addRoR2Background)
                        {
                            outBytes[bIdx + 0] = (byte)bgB;
                            outBytes[bIdx + 1] = (byte)bgG;
                            outBytes[bIdx + 2] = (byte)bgR;
                            outBytes[bIdx + 3] = 255;
                        }
                        else
                        {
                            outBytes[bIdx + 0] = 0;
                            outBytes[bIdx + 1] = 0;
                            outBytes[bIdx + 2] = 0;
                            outBytes[bIdx + 3] = 0;
                        }
                    }
                }
            }

            BitmapData outData = output.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(outBytes, 0, outData.Scan0, outBytes.Length);
            output.UnlockBits(outData);

            return output;
        }

        public static Bitmap Resize(Bitmap src, int w, int h)
        {
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(src, 0, 0, w, h);
            }
            return dst;
        }
    }

    public class Studio
    {
        public static void GenerateAll(string repoRoot, string pluginsDir)
        {
            Console.WriteLine("Rendering all 15 survivor 3D RoR2-style portraits...");

            string outDir = Path.Combine(repoRoot, "mods", "CharacterStatus", "Portraits");
            Directory.CreateDirectory(outDir);

            V3 lightDir = new V3(-0.45f, 0.85f, 0.45f);
            Color ambient = Color.FromArgb(255, 120, 115, 110);

            int renderRes = 512;
            int finalRes = 256;
            float outlineRadius = 11.0f;

            Dictionary<string, Bitmap> transparentPortraits = new Dictionary<string, Bitmap>();
            Dictionary<string, Bitmap> styledPortraits = new Dictionary<string, Bitmap>();

            // 1. THE SURVEYOR (v0.4 NASA astronaut suit, framed properly)
            Console.WriteLine("-> Rendering 3D Surveyor...");
            string svrPath = Path.Combine(repoRoot, "mods", "Surveyor", "surveyor_model.bin.gz");
            HashSet<string> svrBones = new HashSet<string> {
                "head", "head@visor", "neck", "chest", "chest@lidar", "upper_arm.R", "upper_arm.L", "spine", "hand.R@barrels"
            };
            List<Tri> svrTris = ModelLoader.LoadDuelModel(svrPath, svrBones);
            ZRasterizer svrZ = new ZRasterizer(renderRes, renderRes);
            V3 svrTarget = new V3(0.0f, 1.50f, 0.05f);
            V3 svrCam = svrTarget + new V3(-0.95f, 0.18f, 1.95f);
            svrZ.DrawMesh(svrTris, svrCam, svrTarget, 28f, lightDir, ambient);
            using (Bitmap raw = svrZ.ToBitmap())
            {
                transparentPortraits["Surveyor"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["Surveyor"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 2. THE DUELIST
            Console.WriteLine("-> Rendering 3D Duelist...");
            string duelPath = Path.Combine(repoRoot, "src", "Duelist", "duelist_model.bin");
            HashSet<string> duelBones = new HashSet<string> {
                "head", "neck", "chest", "spine", "hips", "upper_arm.R", "upper_arm.L", "forearm.R", "forearm.L", "hand.R", "hand.L"
            };
            List<Tri> duelTris = ModelLoader.LoadDuelModel(duelPath, duelBones);
            ZRasterizer duelZ = new ZRasterizer(renderRes, renderRes);
            V3 duelTarget = new V3(0.0f, 1.35f, -0.05f);
            V3 duelCam = duelTarget + new V3(-0.52f, 0.10f, 1.05f);
            duelZ.DrawMesh(duelTris, duelCam, duelTarget, 30f, lightDir, ambient);
            using (Bitmap raw = duelZ.ToBitmap())
            {
                transparentPortraits["Duelist"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["Duelist"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 3. WILBUR
            Console.WriteLine("-> Rendering 3D Wilbur...");
            string wilburPath = Path.Combine(repoRoot, "src", "WilburMod", "wilbur_model.bin");
            List<Tri> wilburTris = ModelLoader.LoadWilburModel(wilburPath);
            ZRasterizer wilburZ = new ZRasterizer(renderRes, renderRes);
            V3 wilburTarget = new V3(0.0f, 0.42f, 0.12f);
            V3 wilburCam = wilburTarget + new V3(-0.55f, 0.12f, 1.05f);
            wilburZ.DrawMesh(wilburTris, wilburCam, wilburTarget, 32f, lightDir, ambient);
            using (Bitmap raw = wilburZ.ToBitmap())
            {
                transparentPortraits["WilburMod"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["WilburMod"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 4. CLAY REBEL
            Console.WriteLine("-> Rendering 3D Clay Rebel...");
            List<Tri> clayTris = MeshBuilder.BuildClayRebel();
            ZRasterizer clayZ = new ZRasterizer(renderRes, renderRes);
            V3 clayTarget = new V3(0.02f, 1.40f, 0.06f);
            V3 clayCam = clayTarget + new V3(-0.80f, 0.16f, 1.65f);
            clayZ.DrawMesh(clayTris, clayCam, clayTarget, 30f, lightDir, ambient);
            using (Bitmap raw = clayZ.ToBitmap())
            {
                transparentPortraits["ClayRebel"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["ClayRebel"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 5. GRAVITOS
            Console.WriteLine("-> Rendering 3D Gravitos...");
            List<Tri> gravTris = MeshBuilder.BuildGravitos();
            ZRasterizer gravZ = new ZRasterizer(renderRes, renderRes);
            V3 gravTarget = new V3(0.0f, 1.38f, 0.05f);
            V3 gravCam = gravTarget + new V3(-0.52f, 0.12f, 1.10f);
            gravZ.DrawMesh(gravTris, gravCam, gravTarget, 28f, lightDir, ambient);
            using (Bitmap raw = gravZ.ToBitmap())
            {
                transparentPortraits["Gravitos"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["Gravitos"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 6. EMBERWARDEN
            Console.WriteLine("-> Rendering 3D Emberwarden...");
            List<Tri> emberTris = MeshBuilder.BuildEmberwarden();
            ZRasterizer emberZ = new ZRasterizer(renderRes, renderRes);
            V3 emberTarget = new V3(0.0f, 1.36f, 0.05f);
            V3 emberCam = emberTarget + new V3(-0.52f, 0.12f, 1.10f);
            emberZ.DrawMesh(emberTris, emberCam, emberTarget, 28f, lightDir, ambient);
            using (Bitmap raw = emberZ.ToBitmap())
            {
                transparentPortraits["Emberwarden"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, false), finalRes, finalRes);
                styledPortraits["Emberwarden"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(raw, outlineRadius, true), finalRes, finalRes);
            }

            // 7. AUDI TECHNICIAN
            Console.WriteLine("-> Processing 3D Audi Technician...");
            string audiSrcPath = Path.Combine(repoRoot, "mods", "AudiTechnician", "Icons", "portrait.png");
            using (Bitmap src = new Bitmap(audiSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(85, 85, 85), 0.12f, false, 1.0f, 0f, 0f))
            {
                transparentPortraits["AudiTechnician"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, false), finalRes, finalRes);
                styledPortraits["AudiTechnician"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, true), finalRes, finalRes);
            }

            // 8. BILLY BIGSUITS
            Console.WriteLine("-> Processing 3D Billy Bigsuits...");
            string billySrcPath = Path.Combine(repoRoot, "mods", "BillyBigsuits", "Icons", "portrait.png");
            using (Bitmap src = new Bitmap(billySrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(34, 52, 77), 0.12f, false, 1.0f, 0f, 0f))
            {
                transparentPortraits["BillyBigsuits"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, false), finalRes, finalRes);
                styledPortraits["BillyBigsuits"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, true), finalRes, finalRes);
            }

            // 9. CARMAX SPECIALIST (flipped 3/4 right)
            Console.WriteLine("-> Processing 3D CarMax Specialist...");
            string cmxSrcPath = Path.Combine(repoRoot, "launcher", "Assets", "Roster", "CarMaxSpecialist.png");
            using (Bitmap src = new Bitmap(cmxSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(112, 154, 209), 0.16f, true, 1.08f, 0f, -0.04f))
            {
                transparentPortraits["CarMaxSpecialist"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["CarMaxSpecialist"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // 10. BROTHER
            Console.WriteLine("-> Processing 3D Brother...");
            string broSrcPath = Path.Combine(repoRoot, "mods", "OatsBrother", "Icons", "portrait.png");
            using (Bitmap src = new Bitmap(broSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(168, 85, 73), 0.14f, false, 1.15f, 0.05f, 0f))
            {
                transparentPortraits["OatsBrother"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, false), finalRes, finalRes);
                styledPortraits["OatsBrother"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, outlineRadius, true), finalRes, finalRes);
            }

            // 11. ASHWING DRAKE
            Console.WriteLine("-> Processing 3D Ashwing Drake...");
            string ashSrcPath = Path.Combine(repoRoot, "mods", "AshwingDrake", "icon.png");
            using (Bitmap src = new Bitmap(ashSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(140, 160, 175), 0.14f, false, 1.35f, 0.06f, 0.14f))
            {
                transparentPortraits["AshwingDrake"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["AshwingDrake"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // 12. NIDUS (clean chroma segmentation of maroon circle)
            Console.WriteLine("-> Processing 3D Nidus...");
            string nidusSrcPath = Path.Combine(repoRoot, "launcher", "Assets", "Roster", "Nidus.png");
            using (Bitmap src = new Bitmap(nidusSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentChroma(src, c =>
            {
                // Background is dark red/maroon circle or black corners
                bool isMaroon = (c.R > c.G * 1.8f && c.R < 135 && c.G < 48 && c.B < 38);
                bool isCorner = (c.R < 35 && c.G < 20 && c.B < 18);
                return isMaroon || isCorner;
            }, true, 1.40f, 0.04f, 0.18f))
            {
                transparentPortraits["Nidus"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["Nidus"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // 13. PHANTOM ASSASSIN
            Console.WriteLine("-> Processing 3D Phantom Assassin...");
            string phanSrcPath = Path.Combine(repoRoot, "mods", "PhantomAssassin", "icon.png");
            using (Bitmap src = new Bitmap(phanSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentBackground(src, Color.FromArgb(72, 91, 102), 0.14f, true, 1.05f, 0f, 0f))
            {
                transparentPortraits["PhantomAssassin"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["PhantomAssassin"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // 14. INFESTED GENERAL (clean chroma segmentation of purple circle)
            Console.WriteLine("-> Processing 3D Infested General...");
            string infSrcPath = Path.Combine(repoRoot, "launcher", "Assets", "Roster", "InfestedGeneral.png");
            using (Bitmap src = new Bitmap(infSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentChroma(src, c =>
            {
                bool isPurple = (c.B > 22 && c.R > 20 && c.G < 26 && c.B > c.G * 1.4f && (c.R + c.B) > c.G * 2.5f);
                bool isCorner = (c.R < 25 && c.G < 18 && c.B < 32);
                return isPurple || isCorner;
            }, true, 1.36f, 0.02f, 0.14f))
            {
                transparentPortraits["InfestedGeneral"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["InfestedGeneral"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // 15. REAPER (clean chroma segmentation of teal background)
            Console.WriteLine("-> Processing 3D Reaper...");
            string reaperSrcPath = Path.Combine(repoRoot, "release", "reaper_extracted.png");
            if (!File.Exists(reaperSrcPath))
            {
                reaperSrcPath = Path.Combine(repoRoot, "..", "..", "brain", "e6798c8e-4f40-484f-89bb-0fe98abb2454", "scratch", "reaper_extracted.png");
            }
            if (!File.Exists(reaperSrcPath))
            {
                string[] found = Directory.GetFiles(Path.Combine(repoRoot, "..", ".."), "reaper_extracted.png", SearchOption.AllDirectories);
                if (found.Length > 0) reaperSrcPath = found[0];
            }
            using (Bitmap src = new Bitmap(reaperSrcPath))
            using (Bitmap seg = RoR2PortraitProcessor.SegmentChroma(src, c =>
            {
                // Background is teal gradient (keep character dark cloak, only segment teal)
                bool isTeal = (c.B > c.R * 1.32f && c.G > c.R * 1.15f && c.B > 42);
                return isTeal;
            }, true, 1.05f, 0f, 0f))
            {
                transparentPortraits["Reaper"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, false), finalRes, finalRes);
                styledPortraits["Reaper"] = RoR2PortraitProcessor.Resize(RoR2PortraitProcessor.AddRoR2Outline(seg, 5.5f, true), finalRes, finalRes);
            }

            // Save all portraits
            foreach (var kvp in transparentPortraits)
            {
                string key = kvp.Key;
                Bitmap trans = kvp.Value;
                Bitmap styled = styledPortraits[key];

                string csPath = Path.Combine(outDir, key + ".png");
                trans.Save(csPath, ImageFormat.Png);
                Console.WriteLine("Saved: " + csPath);

                string launcherPath = Path.Combine(repoRoot, "launcher", "Assets", "Roster", key + ".png");
                trans.Save(launcherPath, ImageFormat.Png);

                string modDir = Path.Combine(repoRoot, "mods", key);
                if (Directory.Exists(modDir))
                {
                    string p1 = Path.Combine(modDir, "Icons", "portrait.png");
                    if (File.Exists(p1) || Directory.Exists(Path.Combine(modDir, "Icons")))
                    {
                        Directory.CreateDirectory(Path.Combine(modDir, "Icons"));
                        trans.Save(p1, ImageFormat.Png);
                    }
                    string p2 = Path.Combine(modDir, "icon.png");
                    styled.Save(p2, ImageFormat.Png);
                }

                if (!string.IsNullOrEmpty(pluginsDir) && Directory.Exists(pluginsDir))
                {
                    string targetCsDir = Path.Combine(pluginsDir, "CharacterStatus", "Portraits");
                    Directory.CreateDirectory(targetCsDir);
                    trans.Save(Path.Combine(targetCsDir, key + ".png"), ImageFormat.Png);

                    string targetModDir = Path.Combine(pluginsDir, key);
                    if (Directory.Exists(targetModDir))
                    {
                        string p1 = Path.Combine(targetModDir, "Icons", "portrait.png");
                        if (File.Exists(p1) || Directory.Exists(Path.Combine(targetModDir, "Icons")))
                        {
                            Directory.CreateDirectory(Path.Combine(targetModDir, "Icons"));
                            trans.Save(p1, ImageFormat.Png);
                        }
                        string p2 = Path.Combine(targetModDir, "icon.png");
                        styled.Save(p2, ImageFormat.Png);
                    }
                }
            }

            Console.WriteLine("ALL 15 PORTRAITS SUCCESSFULLY GENERATED AND DEPLOYED!");
        }

        public static void Main(string[] args)
        {
            string repoRoot = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            if (!Directory.Exists(Path.Combine(repoRoot, "launcher")))
            {
                repoRoot = @"C:\Users\ahren\.gemini\antigravity\scratch\Broken-Characters-Mod-main";
            }
            string pluginsDir = args.Length > 1 ? args[1] : @"D:\SteamLibrary\steamapps\common\Risk of Rain 2\BepInEx\plugins";
            GenerateAll(repoRoot, pluginsDir);
        }
    }
}
