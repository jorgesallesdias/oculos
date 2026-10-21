#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CetemVR.Editor
{
    /// <summary>
    /// Recorta a ilustração oficial transparente do mascote (1024x949) em partes para animação ("cut-out"):
    /// - Corpo (preservando 100% da transparência original e o logo CETEM na testa)
    /// - Braço que acena (gira no ombro, atrás do corpo)
    /// - Boca aberta (abre/fecha sincronizada com a voz)
    /// - Pálpebras (piscar natural)
    /// </summary>
    public static class MascotSpriteBuilder
    {
        public const string SourcePath = "Assets/_Project/Textures/Mascote_CETEM.png";
        public const string OutFolder = "Assets/_Project/Textures/MascoteRig";
        public const float PixelsPerUnit = 860f; // ~1,05 m de altura no VR

        // Coordenadas medidas na imagem 1024x949 sem fundo (origem no canto SUPERIOR esquerdo)
        private const int ArmMaxX = 290;
        private const int BodyClearMaxX = 284;
        private const int ArmMinY = 40;
        private const int ArmMaxY = 433;
        private static readonly Vector2 ShoulderPx = new Vector2(284f, 412f);
        private static readonly Vector2 FeetPivotPx = new Vector2(485f, 920f);
        private static readonly Vector2 MouthCenterPx = new Vector2(484f, 491f);
        private static readonly Vector2 LidLeftTopPx = new Vector2(414f, 328f);
        private static readonly Vector2 LidRightTopPx = new Vector2(536f, 342f);

        public struct Result
        {
            public Sprite Body;
            public Sprite Arm;
            public Sprite Mouth;
            public Sprite Lid;
            public Vector3 ArmLocalPos;
            public Vector3 MouthLocalPos;
            public Vector3 LidLeftLocalPos;
            public Vector3 LidRightLocalPos;
            public bool Valid;
        }

        public static Result Build()
        {
            var result = new Result();
            Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (src == null) return result;

            if (!Directory.Exists(OutFolder)) Directory.CreateDirectory(OutFolder);

            int w = src.width;
            int h = src.height;
            Color32[] px = src.GetPixels32(); // origem inferior-esquerda

            // Cor da face do cristal (amostrada na bochecha)
            Color32 faceColor = px[Idx(420, 440, w, h)];
            if (faceColor.a < 200) faceColor = new Color32(92, 189, 205, 255);

            // ---------- Corpo ----------
            Color32[] body = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int topY = h - 1 - y;
                    if (x < BodyClearMaxX && topY >= ArmMinY && topY <= ArmMaxY)
                    {
                        body[y * w + x] = new Color32(0, 0, 0, 0);
                    }
                }
            }
            string bodyPath = Save("Mascote_Corpo", body, w, h);

            // ---------- Braço ----------
            int armW = ArmMaxX;
            int armH = ArmMaxY - ArmMinY;
            Color32[] arm = new Color32[armW * armH];
            for (int y = 0; y < armH; y++)
            {
                for (int x = 0; x < armW; x++)
                {
                    int topY = ArmMinY + (armH - 1 - y);
                    arm[y * armW + x] = px[Idx(x, topY, w, h)];
                }
            }
            string armPath = Save("Mascote_Braco", arm, armW, armH);

            // ---------- Boca aberta ----------
            const int mw = 58, mh = 46;
            Color32[] mouth = new Color32[mw * mh];
            Color32 outline = new Color32(25, 25, 28, 255);
            Color32 inside = new Color32(90, 20, 30, 255);
            Color32 tongue = new Color32(240, 110, 130, 255);
            for (int y = 0; y < mh; y++)
            {
                for (int x = 0; x < mw; x++)
                {
                    float nx = (x + 0.5f - mw / 2f) / (mw / 2f);
                    float ny = (y + 0.5f - mh / 2f) / (mh / 2f);
                    float d = nx * nx + (ny > 0 ? ny * ny * 2.2f : ny * ny);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (d <= 1f)
                    {
                        c = d > 0.72f ? outline : (ny < -0.35f && Mathf.Abs(nx) < 0.6f ? tongue : inside);
                    }
                    mouth[y * mw + x] = c;
                }
            }
            string mouthPath = Save("Mascote_Boca", mouth, mw, mh);

            // ---------- Pálpebra ----------
            const int lw = 78, lh = 98;
            Color32[] lid = new Color32[lw * lh];
            for (int y = 0; y < lh; y++)
            {
                for (int x = 0; x < lw; x++)
                {
                    float nx = (x + 0.5f - lw / 2f) / (lw / 2f);
                    float ny = (y + 0.5f - lh / 2f) / (lh / 2f);
                    float d = nx * nx + ny * ny;
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (d <= 1f)
                    {
                        c = (d > 0.8f && ny < 0f) ? outline : faceColor;
                    }
                    lid[y * lw + x] = c;
                }
            }
            string lidPath = Save("Mascote_Palpebra", lid, lw, lh);

            AssetDatabase.Refresh();

            Vector2 bodyPivot = new Vector2(FeetPivotPx.x / w, 1f - FeetPivotPx.y / h);
            ConfigureSprite(bodyPath, bodyPivot);
            ConfigureSprite(armPath, new Vector2(ShoulderPx.x / armW, (ArmMaxY - ShoulderPx.y) / armH));
            ConfigureSprite(mouthPath, new Vector2(0.5f, 0.5f));
            ConfigureSprite(lidPath, new Vector2(0.5f, 1f));

            result.Body = AssetDatabase.LoadAssetAtPath<Sprite>(bodyPath);
            result.Arm = AssetDatabase.LoadAssetAtPath<Sprite>(armPath);
            result.Mouth = AssetDatabase.LoadAssetAtPath<Sprite>(mouthPath);
            result.Lid = AssetDatabase.LoadAssetAtPath<Sprite>(lidPath);

            result.ArmLocalPos = ToLocal(ShoulderPx);
            result.MouthLocalPos = ToLocal(MouthCenterPx);
            result.LidLeftLocalPos = ToLocal(LidLeftTopPx);
            result.LidRightLocalPos = ToLocal(LidRightTopPx);
            result.Valid = result.Body != null;
            return result;
        }

        private static Vector3 ToLocal(Vector2 topLeftPx)
        {
            return new Vector3((topLeftPx.x - FeetPivotPx.x) / PixelsPerUnit, (FeetPivotPx.y - topLeftPx.y) / PixelsPerUnit, 0f);
        }

        private static int Idx(int x, int topY, int w, int h)
        {
            int y = h - 1 - topY;
            return Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1);
        }

        private static string Save(string name, Color32[] pixels, int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            string path = $"{OutFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void ConfigureSprite(string path, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
#endif
