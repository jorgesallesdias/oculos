#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CetemVR.Editor
{
    /// <summary>
    /// Biblioteca de materiais URP compartilhados (salvos como assets para reaproveitar e reduzir draw calls).
    /// </summary>
    public static class CetemMaterialLibrary
    {
        private const string Folder = "Assets/_Project/Materials/Gerados";
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static Material Lit(string name, Color color, float metallic = 0.2f, float smoothness = 0.6f)
        {
            return Get(name, color, metallic, smoothness, Color.black);
        }

        public static Material Glow(string name, Color color, float intensity = 2f)
        {
            return Get(name, color * 0.4f, 0f, 0.5f, color * intensity);
        }

        public static Material LitWithGlow(string name, Color color, Color emission, float metallic, float smoothness)
        {
            return Get(name, color, metallic, smoothness, emission);
        }

        private static Material Get(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            if (Cache.TryGetValue(name, out Material cached) && cached != null) return cached;

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.mat";

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = lit;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            // Emissão sempre habilitada (permite que o script de olhar intensifique o brilho via PropertyBlock)
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", emission);

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }
    }

    /// <summary>
    /// Gera os 4 equipamentos tecnológicos 3D (tamanho real de exibição ~30 cm) que surgem acima de cada mineral:
    /// Neodímio → Aerogerador | Lítio → Bateria | Nióbio → Foguete | Silício → Microchip.
    /// Todas as medidas estão em METROS.
    /// </summary>
    public static class ProceduralTechHolograms
    {
        // ------------------------------------------------------------------
        // Neodímio: Aerogerador (ímãs de neodímio no gerador)
        // ------------------------------------------------------------------
        public static GameObject CreateWindTurbineHologram(Material beamMat, Color theme)
        {
            GameObject root = NewRoot("Holo_Aerogerador_Neodimio");
            Material white = CetemMaterialLibrary.Lit("Tech_Branco", new Color(0.92f, 0.94f, 0.96f), 0.1f, 0.75f);
            Material steel = CetemMaterialLibrary.Lit("Tech_Aco", new Color(0.55f, 0.58f, 0.62f), 0.8f, 0.7f);
            Material glow = CetemMaterialLibrary.Glow("Tech_Glow_Nd", theme, 3f);

            Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.09f, 0.008f, 0.09f), steel);
            Part(PrimitiveType.Cylinder, "Torre", root.transform, new Vector3(0f, -0.03f, 0f), new Vector3(0.022f, 0.12f, 0.022f), white);

            GameObject nacelle = Part(PrimitiveType.Capsule, "Nacele_Gerador", root.transform, new Vector3(0f, 0.10f, 0.01f), new Vector3(0.04f, 0.045f, 0.04f), white);
            nacelle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Anel brilhante = ímãs de neodímio do gerador
            GameObject magnet = Part(PrimitiveType.Cylinder, "Imas_Neodimio", root.transform, new Vector3(0f, 0.10f, 0.012f), new Vector3(0.046f, 0.006f, 0.046f), glow);
            magnet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject hub = new GameObject("Rotor");
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.10f, -0.035f);

            Part(PrimitiveType.Sphere, "Cubo", hub.transform, Vector3.zero, Vector3.one * 0.032f, white);
            for (int i = 0; i < 3; i++)
            {
                GameObject arm = new GameObject($"Pa_{i + 1}");
                arm.transform.SetParent(hub.transform, false);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                GameObject blade = Part(PrimitiveType.Cube, "Lamina", arm.transform, new Vector3(0f, 0.085f, 0f), new Vector3(0.018f, 0.15f, 0.005f), white);
                blade.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            }

            FloatingRotator spin = hub.AddComponent<FloatingRotator>();
            spin.SetRotatorProperties(new Vector3(0f, 0f, -140f), 0f, 0f);

            AddBeam(root.transform, beamMat);
            return Finish(root);
        }

        // ------------------------------------------------------------------
        // Lítio: Bateria de íon-lítio
        // ------------------------------------------------------------------
        public static GameObject CreateBatteryHologram(Material beamMat, Color theme)
        {
            GameObject root = NewRoot("Holo_Bateria_Litio");
            Material shell = CetemMaterialLibrary.Lit("Tech_Bateria_Corpo", new Color(0.08f, 0.10f, 0.12f), 0.6f, 0.8f);
            Material steel = CetemMaterialLibrary.Lit("Tech_Aco", new Color(0.55f, 0.58f, 0.62f), 0.8f, 0.7f);
            Material glow = CetemMaterialLibrary.Glow("Tech_Glow_Li", theme, 3f);

            Part(PrimitiveType.Cylinder, "Corpo", root.transform, Vector3.zero, new Vector3(0.11f, 0.10f, 0.11f), shell);
            Part(PrimitiveType.Cylinder, "Tampa_Inferior", root.transform, new Vector3(0f, -0.10f, 0f), new Vector3(0.112f, 0.006f, 0.112f), steel);
            Part(PrimitiveType.Cylinder, "Tampa_Superior", root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.112f, 0.006f, 0.112f), steel);
            Part(PrimitiveType.Cylinder, "Polo_Positivo", root.transform, new Vector3(0f, 0.115f, 0f), new Vector3(0.04f, 0.012f, 0.04f), steel);

            // Faixas verdes nas bordas
            Part(PrimitiveType.Cylinder, "Faixa_Superior", root.transform, new Vector3(0f, 0.085f, 0f), new Vector3(0.114f, 0.004f, 0.114f), glow);
            Part(PrimitiveType.Cylinder, "Faixa_Inferior", root.transform, new Vector3(0f, -0.085f, 0f), new Vector3(0.114f, 0.004f, 0.114f), glow);

            // Indicador de carga (barras acendendo em sequência) na face voltada ao visitante
            GameObject bars = new GameObject("Indicador_Carga");
            bars.transform.SetParent(root.transform, false);
            bars.transform.localPosition = new Vector3(0f, 0f, -0.056f);
            for (int i = 0; i < 5; i++)
            {
                Part(PrimitiveType.Cube, $"Barra_{i + 1}", bars.transform, new Vector3(0f, -0.06f + i * 0.03f, 0f), new Vector3(0.05f, 0.02f, 0.004f), glow);
            }
            bars.AddComponent<HologramPulse>().Configure(HologramPulse.Mode.Sequential, 3f, 0f);

            // Elétrons orbitando
            GameObject orbit = new GameObject("Orbita_Eletrons");
            orbit.transform.SetParent(root.transform, false);
            orbit.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Part(PrimitiveType.Sphere, $"Eletron_{i + 1}", orbit.transform, new Vector3(Mathf.Cos(a) * 0.1f, 0f, Mathf.Sin(a) * 0.1f), Vector3.one * 0.016f, glow);
            }
            orbit.AddComponent<FloatingRotator>().SetRotatorProperties(new Vector3(0f, 90f, 0f), 0f, 0f);

            AddBeam(root.transform, beamMat);
            return Finish(root);
        }

        // ------------------------------------------------------------------
        // Nióbio: Foguete (bocal de superliga de nióbio)
        // ------------------------------------------------------------------
        public static GameObject CreateRocketThrusterHologram(Material beamMat, Color theme)
        {
            GameObject root = NewRoot("Holo_Foguete_Niobio");
            Material white = CetemMaterialLibrary.Lit("Tech_Branco", new Color(0.92f, 0.94f, 0.96f), 0.1f, 0.75f);
            Material niobium = CetemMaterialLibrary.LitWithGlow("Tech_Niobio_Bocal", new Color(0.45f, 0.30f, 0.65f), new Color(0.25f, 0.1f, 0.45f), 0.9f, 0.85f);
            Material accent = CetemMaterialLibrary.Glow("Tech_Glow_Nb", theme, 2.5f);
            Material flame = CetemMaterialLibrary.Glow("Tech_Chama", new Color(1f, 0.55f, 0.15f), 4f);

            Part(PrimitiveType.Cylinder, "Fuselagem", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.06f, 0.09f, 0.06f), white);
            Part(PrimitiveType.Sphere, "Ogiva", root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.06f, 0.10f, 0.06f), white);
            Part(PrimitiveType.Cylinder, "Faixa", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.062f, 0.005f, 0.062f), accent);
            Part(PrimitiveType.Sphere, "Janela", root.transform, new Vector3(0f, 0.04f, -0.028f), new Vector3(0.022f, 0.022f, 0.01f), accent);

            for (int i = 0; i < 3; i++)
            {
                GameObject finPivot = new GameObject($"Aleta_{i + 1}");
                finPivot.transform.SetParent(root.transform, false);
                finPivot.transform.localRotation = Quaternion.Euler(0f, i * 120f + 60f, 0f);
                Part(PrimitiveType.Cube, "Aleta", finPivot.transform, new Vector3(0f, -0.06f, 0.04f), new Vector3(0.004f, 0.05f, 0.035f), accent);
            }

            // Bocal de nióbio (cone aproximado com 2 cilindros)
            Part(PrimitiveType.Cylinder, "Bocal_Niobio_1", root.transform, new Vector3(0f, -0.09f, 0f), new Vector3(0.04f, 0.012f, 0.04f), niobium);
            Part(PrimitiveType.Cylinder, "Bocal_Niobio_2", root.transform, new Vector3(0f, -0.11f, 0f), new Vector3(0.055f, 0.012f, 0.055f), niobium);

            GameObject fire = Part(PrimitiveType.Sphere, "Chama", root.transform, new Vector3(0f, -0.16f, 0f), new Vector3(0.04f, 0.07f, 0.04f), flame);
            fire.AddComponent<HologramPulse>().Configure(HologramPulse.Mode.Pulse, 14f, 0.18f);

            AddBeam(root.transform, beamMat);
            return Finish(root);
        }

        // ------------------------------------------------------------------
        // Silício: Microchip + célula solar
        // ------------------------------------------------------------------
        public static GameObject CreateMicrochipHologram(Material beamMat, Color theme)
        {
            GameObject root = NewRoot("Holo_Microchip_Silicio");
            Material board = CetemMaterialLibrary.Lit("Tech_Placa_Verde", new Color(0.05f, 0.25f, 0.15f), 0.2f, 0.6f);
            Material die = CetemMaterialLibrary.Lit("Tech_Chip_Preto", new Color(0.06f, 0.06f, 0.07f), 0.5f, 0.85f);
            Material gold = CetemMaterialLibrary.LitWithGlow("Tech_Ouro", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0.02f), 1f, 0.85f);
            Material glow = CetemMaterialLibrary.Glow("Tech_Glow_Si", theme, 3f);
            Material solar = CetemMaterialLibrary.LitWithGlow("Tech_Celula_Solar", new Color(0.08f, 0.15f, 0.45f), new Color(0.02f, 0.05f, 0.2f), 0.7f, 0.9f);

            // Chip inclinado em direção ao visitante
            GameObject chip = new GameObject("Microchip");
            chip.transform.SetParent(root.transform, false);
            chip.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            chip.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);

            Part(PrimitiveType.Cube, "Placa", chip.transform, Vector3.zero, new Vector3(0.20f, 0.012f, 0.20f), board);
            Part(PrimitiveType.Cube, "Encapsulamento", chip.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.11f, 0.014f, 0.11f), die);
            Part(PrimitiveType.Cube, "Nucleo_Silicio", chip.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.004f, 0.05f), glow);

            // Pinos dourados nos 4 lados
            for (int side = 0; side < 4; side++)
            {
                GameObject sidePivot = new GameObject($"Pinos_{side + 1}");
                sidePivot.transform.SetParent(chip.transform, false);
                sidePivot.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                for (int i = 0; i < 6; i++)
                {
                    float x = -0.045f + i * 0.018f;
                    Part(PrimitiveType.Cube, "Pino", sidePivot.transform, new Vector3(x, 0.008f, 0.068f), new Vector3(0.006f, 0.003f, 0.026f), gold);
                }
            }

            // Trilhas brilhantes do circuito
            Part(PrimitiveType.Cube, "Trilha_1", chip.transform, new Vector3(0.0f, 0.0065f, 0.088f), new Vector3(0.16f, 0.002f, 0.004f), glow);
            Part(PrimitiveType.Cube, "Trilha_2", chip.transform, new Vector3(0.0f, 0.0065f, -0.088f), new Vector3(0.16f, 0.002f, 0.004f), glow);

            // Pequena célula solar atrás
            GameObject panel = Part(PrimitiveType.Cube, "Celula_Solar", root.transform, new Vector3(0f, 0.12f, 0.06f), new Vector3(0.16f, 0.005f, 0.10f), solar);
            panel.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            AddBeam(root.transform, beamMat);
            return Finish(root);
        }

        // ------------------------------------------------------------------
        // Utilitários
        // ------------------------------------------------------------------

        private static GameObject NewRoot(string name)
        {
            return new GameObject(name);
        }

        private static GameObject Finish(GameObject root)
        {
            // Escala natural = 1 (as peças já estão em metros). O efeito cresce de 0 até 1 ao surgir.
            root.AddComponent<HologramVisualEffect>();
            return root;
        }

        /// <summary>Feixe desativado para não encobrir a foto real do mineral abaixo do equipamento 3D.</summary>
        private static void AddBeam(Transform root, Material beamMat)
        {
            // Removido cilindro Feixe_Projetor para manter a visão limpa da imagem do mineral e da placa.
        }

        public static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }
    }
}
#endif
