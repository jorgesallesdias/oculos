#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CetemVR.Editor
{
    /// <summary>
    /// Monta automaticamente a cena completa do laboratório CETEM VR:
    /// - Bancada curva posicionada a distância confortável (~1,85 m) com arco de 54° (-27° a +27°),
    ///   garantindo que do 1º ao 4º mineral todos fiquem visíveis de uma só vez no campo de visão do Quest 2.
    /// - Mascote oficial transparente recortado em partes: voz natural em português (sem bipes robóticos),
    ///   boca sincronizada com a voz (lip-sync), olhos piscando e braço acenando.
    /// - Letras grandes e de alto contraste nos cartões da Tabela Periódica e no balão de fala.
    /// </summary>
    public static class CetemSceneBuilder
    {
        private const string BasePath = "Assets/_Project";
        private const string ScriptableObjectsPath = BasePath + "/ScriptableObjects";
        private const string MaterialsPath = BasePath + "/Materials";
        private const string PrefabsPath = BasePath + "/Prefabs";
        private const string ScenesPath = BasePath + "/Scenes";
        private const string TexturesPath = BasePath + "/Textures";
        private const string MeshesPath = BasePath + "/Meshes";
        private const string AudioPath = BasePath + "/Audio";
        private const string MascotTexturePath = TexturesPath + "/Mascote_CETEM.png";
        private const string SceneFile = ScenesPath + "/Capsula_Terras_Raras_CETEM.unity";

        // Layout (metros, referência: olhos do visitante a 1,55 m na origem)
        private const float BenchRadius = 2.45f;
        private const float BenchTopY = 0.88f;
        private const float UiScale = 0.00125f;

        private static Font _font;

        // =========================================================================
        // MENUS
        // =========================================================================

        [MenuItem("CETEM VR/1. Gerar Assets dos 4 Minerais (ScriptableObjects)", false, 10)]
        public static void GenerateMineralAssets()
        {
            EnsureDirectories();
            ConfigureVoiceClips();

            AudioClip voNd = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_neodimio.mp3");
            AudioClip voLi = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_litio.mp3");
            AudioClip voNb = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_niobio.mp3");
            AudioClip voSi = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_silicio.mp3");

            CreateOrUpdateMineral("NeodimioData", "Neodímio", "Nd", "Terras Raras • Superímãs",
                "Usado para criar superímãs potentes que geram energia limpa nas turbinas eólicas e movem carros elétricos e fones de ouvido!",
                new Color(0f, 0.85f, 1f, 1f), voNd);

            CreateOrUpdateMineral("LitioData", "Lítio", "Li", "Baterias • Carros Elétricos",
                "O metal mais leve que existe! É o coração das baterias recarregáveis de celulares, notebooks e carros elétricos!",
                new Color(0.25f, 1f, 0.2f, 1f), voLi);

            CreateOrUpdateMineral("NiobioData", "Nióbio", "Nb", "Superligas • Foguetes",
                "O Brasil tem as maiores reservas do mundo! Deixa o aço super resistente ao calor para foguetes espaciais, aviões e pontes!",
                new Color(0.7f, 0.4f, 1f, 1f), voNb);

            CreateOrUpdateMineral("QuartzoSilicioData", "Quartzo / Silício", "Si", "Microchips • Energia Solar",
                "Do quartzo tiramos o silício de alta pureza, base dos microchips de computadores, celulares e das placas solares!",
                new Color(1f, 0.75f, 0.1f, 1f), voSi);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("CETEM VR/2. Montar Cena Completa (Laboratório VR)", false, 20)]
        public static void BuildCompleteVRScene()
        {
            BuildCompleteVRSceneInternal(showDialog: true);
        }

        private static void BuildCompleteVRSceneInternal(bool showDialog)
        {
            EnsureDirectories();
            GenerateMineralAssets();
            PrepareMascotSourceImporter();
            CetemMaterialLibrary.ClearCache();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Iluminação ----------------
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.38f, 0.46f);

            GameObject dirLightObj = new GameObject("Directional Light");
            Light dirLight = dirLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.05f;
            dirLight.color = new Color(0.95f, 0.97f, 1.0f);
            dirLight.shadows = LightShadows.None;
            dirLightObj.transform.rotation = Quaternion.Euler(50f, 20f, 0f);

            // ---------------- Raiz do cenário (reposicionável) ----------------
            GameObject sceneRoot = new GameObject("Cenario_CETEM");
            ExperienceAnchor anchor = sceneRoot.AddComponent<ExperienceAnchor>();

            CreateLabEnvironment(sceneRoot.transform);
            CreateCurvedBench(sceneRoot.transform);

            // ---------------- Minerais (arco de -24° a +24° a ~2,57m: todos os 4 visíveis com conforto) ----------------
            MineralData nd = AssetDatabase.LoadAssetAtPath<MineralData>($"{ScriptableObjectsPath}/NeodimioData.asset");
            MineralData li = AssetDatabase.LoadAssetAtPath<MineralData>($"{ScriptableObjectsPath}/LitioData.asset");
            MineralData nb = AssetDatabase.LoadAssetAtPath<MineralData>($"{ScriptableObjectsPath}/NiobioData.asset");
            MineralData si = AssetDatabase.LoadAssetAtPath<MineralData>($"{ScriptableObjectsPath}/QuartzoSilicioData.asset");

            Shader holoShader = Shader.Find("CetemVR/HologramURP") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material beamNd = CreateHoloMaterial("Mat_Feixe_Nd", holoShader, nd.highlightColor * 0.5f);
            Material beamLi = CreateHoloMaterial("Mat_Feixe_Li", holoShader, li.highlightColor * 0.5f);
            Material beamNb = CreateHoloMaterial("Mat_Feixe_Nb", holoShader, nb.highlightColor * 0.5f);
            Material beamSi = CreateHoloMaterial("Mat_Feixe_Si", holoShader, si.highlightColor * 0.5f);

            Sprite sprNd = GetRealMineralSprite("Mineral_Neodimio.png");
            Sprite sprLi = GetRealMineralSprite("Mineral_Litio.png");
            Sprite sprNb = GetRealMineralSprite("Mineral_Niobio.png");
            Sprite sprSi = GetRealMineralSprite("Mineral_Silicio.png");

            GameObject stationsRoot = new GameObject("Estacoes_Minerais");
            stationsRoot.transform.SetParent(sceneRoot.transform, false);

            var minerals = new List<GazeInteractableMineral>
            {
                CreateMineralStation("Estacao_1_Neodimio", -21f, nd, "60", "Nd", "Neodímio", "Superímãs • Eólica",
                    ProceduralTechHolograms.CreateWindTurbineHologram(beamNd, nd.highlightColor), sprNd, stationsRoot.transform),
                CreateMineralStation("Estacao_2_Litio", -7f, li, "3", "Li", "Lítio", "Baterias • Elétricos",
                    ProceduralTechHolograms.CreateBatteryHologram(beamLi, li.highlightColor), sprLi, stationsRoot.transform),
                CreateMineralStation("Estacao_3_Niobio", 7f, nb, "41", "Nb", "Nióbio", "Superligas • Foguetes",
                    ProceduralTechHolograms.CreateRocketThrusterHologram(beamNb, nb.highlightColor), sprNb, stationsRoot.transform),
                CreateMineralStation("Estacao_4_Silicio", 21f, si, "14", "Si", "Silício", "Microchips • Solar",
                    ProceduralTechHolograms.CreateMicrochipHologram(beamSi, si.highlightColor), sprSi, stationsRoot.transform),
            };

            // ---------------- Mascote (com placa única de mensagens e fala) ----------------
            AudioClip introClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_intro.mp3");
            AudioClip outroClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/vo_outro.mp3");
            CetemMascotController mascot = CreateMascotCharacter(sceneRoot.transform, introClip, outroClip);

            // ---------------- XR Origin ----------------
            GameObject xrOrigin = new GameObject("XR Origin (VR)");
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrOrigin.transform, false);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(cameraOffset.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();

            var origin = xrOrigin.AddComponent<Unity.XR.CoreUtils.XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;

            Image reticleFill = CreateGazeReticle(camObj.transform);
            CreateControllerPointer("Ponteiro_Esquerdo", UnityEngine.XR.XRNode.LeftHand, cameraOffset.transform);
            CreateControllerPointer("Ponteiro_Direito", UnityEngine.XR.XRNode.RightHand, cameraOffset.transform);

            SetSerializedField(anchor, "vrCameraTransform", camObj.transform);

            // ---------------- ExperienceManager ----------------
            GameObject mgrObj = new GameObject("ExperienceManager");
            var mgr = mgrObj.AddComponent<ExperienceManager>();
            var sfx = mgrObj.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            SetSerializedField(mgr, "vrCameraTransform", camObj.transform);
            SetSerializedField(mgr, "benchMinerals", minerals);
            SetSerializedField(mgr, "mascotController", mascot);
            SetSerializedField(mgr, "mascotAudioSource", mascot.GetComponent<AudioSource>());
            SetSerializedField(mgr, "introWelcomeClip", introClip);
            SetSerializedField(mgr, "outroConclusionClip", outroClip);
            SetSerializedField(mgr, "experienceAnchor", anchor);
            SetSerializedField(mgr, "sfxAudioSource", sfx);
            SetSerializedField(mgr, "globalReticleFillImage", reticleFill);

            EditorSceneManager.SaveScene(scene, SceneFile);
            AddSceneToBuildSettings(SceneFile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CETEM VR] Cena montada com sucesso: " + SceneFile);
            if (!Application.isBatchMode && showDialog)
            {
                EditorUtility.DisplayDialog("CETEM VR", "Laboratório atualizado com sucesso!\n\n- Imagens reais dos 4 minerais no lugar dos losangos.\n- Bancada mais afastada (~2,5 m) com visão ampla dos 4 elementos.\n- Clique/olhar apenas no Mascote para iniciar o vídeo automático.\n- Stop automático ao retirar o óculos.", "OK");
            }
        }

        [MenuItem("CETEM VR/3. Abrir Janela de Build", false, 30)]
        public static void OpenBuildWindow()
        {
            EditorApplication.ExecuteMenuItem("File/Build Profiles");
        }

        [MenuItem("CETEM VR/4. Compilar e Enviar Direto para o Óculos (1 Clique)", false, 40)]
        public static void BuildAndRunDirectToQuest()
        {
            BuildInternal(autoRun: true);
        }

        [MenuItem("CETEM VR/5. Apenas Compilar APK do Projeto", false, 50)]
        public static void BuildApkOnly()
        {
            BuildInternal(autoRun: false);
        }

        private static void BuildInternal(bool autoRun)
        {
            // Reconstrói e salva a cena automaticamente antes de gerar o APK para garantir que tudo esteja atualizado!
            BuildCompleteVRSceneInternal(showDialog: false);

            EnsureDirectory("Builds");
            const string outputApk = "Builds/CETEM_VR.apk";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneFile },
                locationPathName = outputApk,
                target = BuildTarget.Android,
                options = autoRun ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };

            Debug.Log($"[CETEM VR] Compilando APK (AutoRun={autoRun})...");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[CETEM VR] APK gerado: {outputApk} ({report.summary.totalSize / (1024 * 1024)} MB)");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("CETEM VR - Sucesso!",
                        autoRun ? "Aplicativo instalado no Meta Quest 2! Coloque o óculos." : $"APK gerado em:\n{outputApk}", "OK");
                }
            }
            else
            {
                Debug.LogError($"[CETEM VR] Erro na compilação: {report.summary.totalErrors} erros.");
            }
        }

        // =========================================================================
        // AMBIENTE
        // =========================================================================

        private static void CreateLabEnvironment(Transform parent)
        {
            GameObject env = new GameObject("Ambiente_Laboratorio");
            env.transform.SetParent(parent, false);

            Material floorMat = CetemMaterialLibrary.Lit("Lab_Piso", new Color(0.07f, 0.09f, 0.12f), 0.3f, 0.85f);
            Material wallMat = CetemMaterialLibrary.Lit("Lab_Parede", new Color(0.10f, 0.13f, 0.18f), 0.1f, 0.4f);
            Material panelMat = CetemMaterialLibrary.Lit("Lab_Painel", new Color(0.14f, 0.18f, 0.24f), 0.5f, 0.6f);
            Material neon = CetemMaterialLibrary.Glow("Lab_Neon_Ciano", new Color(0f, 0.8f, 1f), 2.5f);

            Prim(PrimitiveType.Cylinder, "Piso", env.transform, new Vector3(0f, -0.01f, 1.5f), new Vector3(11f, 0.01f, 11f), floorMat);
            Prim(PrimitiveType.Cylinder, "Anel_Piso", env.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.9f, 0.002f, 0.9f), neon);
            Prim(PrimitiveType.Cylinder, "Anel_Piso_Miolo", env.transform, new Vector3(0f, 0.004f, 0f), new Vector3(0.86f, 0.002f, 0.86f), floorMat);

            const float wallRadius = 4.9f;
            for (int i = -4; i <= 4; i++)
            {
                float a = i * 15f;
                Quaternion rot = Quaternion.Euler(0f, a, 0f);
                Vector3 pos = rot * new Vector3(0f, 1.6f, wallRadius);
                GameObject wall = Prim(PrimitiveType.Cube, $"Parede_{i + 5}", env.transform, pos, new Vector3(1.32f, 3.2f, 0.1f), wallMat);
                wall.transform.localRotation = rot;

                GameObject panel = Prim(PrimitiveType.Cube, "Painel_Recuado", wall.transform, new Vector3(0f, 0.05f, -0.6f), new Vector3(0.85f, 0.5f, 0.5f), panelMat);
                GameObject strip = Prim(PrimitiveType.Cube, "Neon", wall.transform, new Vector3(0f, 0.36f, -0.7f), new Vector3(0.98f, 0.006f, 0.3f), neon);
            }

            Prim(PrimitiveType.Cylinder, "Luminaria_Teto", env.transform, new Vector3(0f, 3.2f, 2.3f), new Vector3(2.8f, 0.01f, 2.8f), neon);
        }

        private static void CreateCurvedBench(Transform parent)
        {
            GameObject bench = new GameObject("Bancada_Curva");
            bench.transform.SetParent(parent, false);

            Material top = CetemMaterialLibrary.Lit("Bancada_Tampo", new Color(0.16f, 0.19f, 0.24f), 0.6f, 0.85f);
            Material body = CetemMaterialLibrary.Lit("Bancada_Corpo", new Color(0.09f, 0.11f, 0.15f), 0.4f, 0.6f);
            Material neon = CetemMaterialLibrary.Glow("Lab_Neon_Ciano", new Color(0f, 0.8f, 1f), 2.5f);

            const float step = 8f;
            const float depth = 0.62f;
            float segWidth = 2f * (BenchRadius + depth) * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.015f;

            for (float a = -36f; a <= 36.01f; a += step)
            {
                Quaternion rot = Quaternion.Euler(0f, a, 0f);
                GameObject seg = new GameObject($"Segmento_{a:0}");
                seg.transform.SetParent(bench.transform, false);
                seg.transform.localRotation = rot;

                float mid = BenchRadius + 0.12f;
                Prim(PrimitiveType.Cube, "Tampo", seg.transform, new Vector3(0f, BenchTopY - 0.025f, mid), new Vector3(segWidth, 0.05f, depth), top);
                Prim(PrimitiveType.Cube, "Corpo", seg.transform, new Vector3(0f, (BenchTopY - 0.05f) * 0.5f, mid + 0.05f), new Vector3(segWidth * 0.98f, BenchTopY - 0.05f, depth - 0.12f), body);
                Prim(PrimitiveType.Cube, "Friso_Neon", seg.transform, new Vector3(0f, BenchTopY - 0.035f, mid - depth * 0.5f - 0.003f), new Vector3(segWidth, 0.014f, 0.006f), neon);
                Prim(PrimitiveType.Cube, "Friso_Rodape", seg.transform, new Vector3(0f, 0.06f, mid - depth * 0.5f + 0.06f), new Vector3(segWidth, 0.012f, 0.006f), neon);
            }
        }

        // =========================================================================
        // ESTAÇÕES DOS MINERAIS (COM IMAGEM REAL DE CADA MINERAL)
        // =========================================================================

        private static GazeInteractableMineral CreateMineralStation(
            string name, float angleDeg, MineralData data,
            string atomicNumber, string symbol, string title, string subtitle,
            GameObject techHologram, Sprite mineralSprite, Transform parent)
        {
            Color theme = data != null ? data.highlightColor : Color.cyan;
            string key = symbol;

            Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
            GameObject station = new GameObject(name);
            station.transform.SetParent(parent, false);
            station.transform.localPosition = rot * new Vector3(0f, BenchTopY, BenchRadius + 0.12f);
            station.transform.localRotation = rot;

            Material dark = CetemMaterialLibrary.Lit("Estacao_Base", new Color(0.06f, 0.07f, 0.09f), 0.7f, 0.8f);
            Material ring = CetemMaterialLibrary.Glow($"Estacao_Anel_{key}", theme, 2.5f);

            // Base futurista na bancada
            Prim(PrimitiveType.Cylinder, "Base_Projetor", station.transform, new Vector3(0f, 0.010f, -0.12f), new Vector3(0.32f, 0.010f, 0.24f), dark);
            Prim(PrimitiveType.Cylinder, "Anel_Luz", station.transform, new Vector3(0f, 0.021f, -0.12f), new Vector3(0.28f, 0.003f, 0.20f), ring);

            // Suporte físico da Placa da Tabela Periódica (apoiado e fixo na bancada)
            Prim(PrimitiveType.Cube, "Suporte_Placa", station.transform, new Vector3(0f, 0.010f, -0.17f), new Vector3(0.40f, 0.020f, 0.05f), dark);
            Prim(PrimitiveType.Cube, "Friso_Suporte", station.transform, new Vector3(0f, 0.020f, -0.195f), new Vector3(0.40f, 0.006f, 0.006f), ring);

            // Placa da Tabela Periódica (apoiada na bancada, 0 a 23 cm de altura)
            CreatePeriodicTableCard(station.transform, theme, atomicNumber, symbol, title, subtitle);

            // Imagem real do mineral flutuando BEM ACIMA da placa da Tabela Periódica (32 a 54 cm de altura, sem cobrir nada!)
            GameObject mineralObj = new GameObject("Imagem_Mineral_Real");
            mineralObj.transform.SetParent(station.transform, false);
            mineralObj.transform.localPosition = new Vector3(0f, 0.43f, -0.10f);
            mineralObj.transform.localRotation = Quaternion.identity;
            mineralObj.transform.localScale = Vector3.one * 0.72f;

            var sr = mineralObj.AddComponent<SpriteRenderer>();
            sr.sprite = mineralSprite;
            sr.sortingOrder = 15;

            FloatingRotator rotator = mineralObj.AddComponent<FloatingRotator>();
            // Apenas flutuação vertical suave (sem girar 360° para manter a foto do mineral sempre de frente)
            rotator.SetRotatorProperties(Vector3.zero, 0.010f, 1.5f);

            // Equipamento 3D tecnológico posicionado acima da foto do mineral
            GameObject holoPoint = new GameObject("HologramSpawnPoint");
            holoPoint.transform.SetParent(station.transform, false);
            holoPoint.transform.localPosition = new Vector3(0f, 0.80f, -0.05f);
            holoPoint.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (techHologram != null)
            {
                techHologram.transform.SetParent(holoPoint.transform, false);
                techHologram.transform.localPosition = Vector3.zero;
                var fx = techHologram.GetComponent<HologramVisualEffect>();
                if (fx != null) fx.SetTargetScale(Vector3.one * 1.30f);
                techHologram.SetActive(false);
            }

            // Luz de destaque quando o mascote está apresentando este mineral
            GameObject lightObj = new GameObject("Luz_Destaque");
            lightObj.transform.SetParent(station.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.42f, -0.20f);
            Light pl = lightObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 1.4f;
            pl.intensity = 2.4f;
            pl.color = theme;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            var interactable = station.AddComponent<GazeInteractableMineral>();
            SetSerializedField(interactable, "mineralData", data);
            SetSerializedField(interactable, "hologramSpawnPoint", holoPoint.transform);
            SetSerializedField(interactable, "crystalFloatingRotator", rotator);
            SetSerializedField(interactable, "highlightLight", pl);
            SetSerializedField(interactable, "crystalRenderer", sr);
            return interactable;
        }

        private static GameObject CreatePeriodicTableCard(Transform parent, Color theme, string z, string symbol, string title, string subtitle)
        {
            GameObject cardRoot = new GameObject("Placa_Tabela_Periodica");
            cardRoot.transform.SetParent(parent, false);

            Canvas canvas = cardRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = cardRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(340f, 210f);
            rt.pivot = new Vector2(0.5f, 0.0f); // Pivot na base apoia a placa exatamente sobre a bancada (sem somar offset extra nos filhos!)
            cardRoot.transform.localPosition = new Vector3(0f, 0.015f, -0.17f);
            cardRoot.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            rt.localScale = Vector3.one * 0.0011f;

            UIImage(cardRoot.transform, "Borda", new Vector2(348f, 218f), Vector2.zero, new Color(theme.r, theme.g, theme.b, 0.95f));
            UIImage(cardRoot.transform, "Fundo", new Vector2(336f, 206f), Vector2.zero, new Color(0.02f, 0.04f, 0.08f, 0.97f));

            UIText(cardRoot.transform, "Numero", z, 36, FontStyle.Bold, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperLeft, new Vector2(90f, 40f), new Vector2(-110f, 74f));
            UIText(cardRoot.transform, "Simbolo", symbol, 84, FontStyle.Bold, theme, TextAnchor.MiddleCenter, new Vector2(260f, 88f), new Vector2(0f, 22f));
            UIText(cardRoot.transform, "Nome", title, 38, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(320f, 42f), new Vector2(0f, -38f));
            UIText(cardRoot.transform, "Uso", subtitle, 26, FontStyle.Bold, new Color(0.78f, 0.92f, 1f), TextAnchor.MiddleCenter, new Vector2(320f, 36f), new Vector2(0f, -78f));

            return cardRoot;
        }

        // =========================================================================
        // MASCOTE
        // =========================================================================

        private static CetemMascotController CreateMascotCharacter(Transform parent, AudioClip introClip, AudioClip outroClip)
        {
            GameObject root = new GameObject("Mascote_CETEM");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-0.48f, 0.85f, 3.25f);

            Material dark = CetemMaterialLibrary.Lit("Estacao_Base", new Color(0.06f, 0.07f, 0.09f), 0.7f, 0.8f);
            Material neon = CetemMaterialLibrary.Glow("Lab_Neon_Ciano", new Color(0f, 0.8f, 1f), 2.5f);

            Prim(PrimitiveType.Cylinder, "Plataforma", parent, root.transform.localPosition + new Vector3(0f, -0.425f, 0f), new Vector3(0.7f, 0.425f, 0.7f), dark);
            Prim(PrimitiveType.Cylinder, "Plataforma_Neon", parent, root.transform.localPosition + new Vector3(0f, -0.005f, 0f), new Vector3(0.72f, 0.006f, 0.72f), neon);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            MascotSpriteBuilder.Result rig = MascotSpriteBuilder.Build();

            Transform arm = null, mouth = null, lidL = null, lidR = null;
            if (rig.Valid)
            {
                AddSprite(visual.transform, "Corpo", rig.Body, Vector3.zero, 0);
                arm = AddSprite(visual.transform, "Braco_Aceno", rig.Arm, rig.ArmLocalPos + new Vector3(0f, 0f, 0.002f), -1).transform;
                mouth = AddSprite(visual.transform, "Boca", rig.Mouth, rig.MouthLocalPos + new Vector3(0f, 0f, -0.002f), 2).transform;
                lidL = AddSprite(visual.transform, "Palpebra_E", rig.Lid, rig.LidLeftLocalPos + new Vector3(0f, 0f, -0.002f), 2).transform;
                lidR = AddSprite(visual.transform, "Palpebra_D", rig.Lid, rig.LidRightLocalPos + new Vector3(0f, 0f, -0.002f), 2).transform;
            }
            else
            {
                Sprite full = AssetDatabase.LoadAssetAtPath<Sprite>(MascotTexturePath);
                AddSprite(visual.transform, "Corpo", full, new Vector3(0f, 0.52f, 0f), 0);
            }

            // Placa Única de Fala / Notícia grande e legível ao lado do mascote (sem nenhum painel atrás)
            GameObject bubble = new GameObject("Balao_Fala");
            bubble.transform.SetParent(parent, false);
            Vector3 bubblePos = new Vector3(0.58f, 1.96f, 3.12f);
            bubble.transform.localPosition = bubblePos;
            bubble.transform.localRotation = Quaternion.Euler(0f, 11f, 0f);

            Canvas c = bubble.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            CanvasGroup group = bubble.AddComponent<CanvasGroup>();
            RectTransform brt = bubble.GetComponent<RectTransform>();
            brt.sizeDelta = new Vector2(1080f, 540f);
            brt.localScale = Vector3.one * 0.00135f;

            UIImage(bubble.transform, "Borda", new Vector2(1092f, 552f), Vector2.zero, new Color(0f, 0.85f, 1f, 0.95f));
            UIImage(bubble.transform, "Fundo", new Vector2(1076f, 536f), Vector2.zero, new Color(0.02f, 0.04f, 0.09f, 0.97f));
            Image tail = UIImage(bubble.transform, "Ponta", new Vector2(54f, 54f), new Vector2(-542f, -160f), new Color(0f, 0.85f, 1f, 0.95f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Text titleText = UIText(bubble.transform, "Titulo", "", 60, FontStyle.Bold, new Color(0.35f, 0.95f, 1f), TextAnchor.UpperLeft, new Vector2(1000f, 80f), new Vector2(0f, 195f));
            Text bodyText = UIText(bubble.transform, "Texto", "", 46, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, new Vector2(1000f, 390f), new Vector2(0f, -45f));
            bodyText.lineSpacing = 1.15f;

            // Collider amplo e generoso no corpo e no botão do Mascote para início garantido via olhar ou clique
            BoxCollider mascotCol = root.AddComponent<BoxCollider>();
            mascotCol.center = new Vector3(0f, 0.40f, -0.15f);
            mascotCol.size = new Vector3(1.10f, 1.40f, 0.80f);
            mascotCol.isTrigger = true;

            // Botão / Crachá interativo "OLHE OU CLIQUE NO MASCOTE PARA COMEÇAR" na frente do mascote
            GameObject startBadge = new GameObject("Botao_Iniciar_Mascote");
            startBadge.transform.SetParent(root.transform, false);
            startBadge.transform.localPosition = new Vector3(0f, 0.06f, -0.32f);
            startBadge.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

            Canvas badgeCanvas = startBadge.AddComponent<Canvas>();
            badgeCanvas.renderMode = RenderMode.WorldSpace;
            badgeCanvas.sortingOrder = 10;
            RectTransform badgeRt = startBadge.GetComponent<RectTransform>();
            badgeRt.sizeDelta = new Vector2(760f, 160f);
            badgeRt.localScale = Vector3.one * 0.0011f;

            UIImage(startBadge.transform, "Borda", new Vector2(772f, 172f), Vector2.zero, new Color(0f, 0.95f, 1f, 0.98f));
            UIImage(startBadge.transform, "Fundo", new Vector2(756f, 156f), Vector2.zero, new Color(0.02f, 0.06f, 0.14f, 0.97f));

            Text badgeLabel = UIText(startBadge.transform, "Label", "OLHE OU CLIQUE NO MASCOTE PARA COMEÇAR", 36, FontStyle.Bold, new Color(0.4f, 1f, 0.9f), TextAnchor.MiddleCenter, new Vector2(610f, 130f), new Vector2(-45f, 0f));

            Image badgeTrack = UIImage(startBadge.transform, "Anel_Fundo", new Vector2(88f, 88f), new Vector2(310f, 0f), new Color(1f, 1f, 1f, 0.22f));
            badgeTrack.sprite = GetUiSprite("UI_Anel");
            Image badgeFill = UIImage(startBadge.transform, "Anel_Progresso", new Vector2(88f, 88f), new Vector2(310f, 0f), new Color(0.2f, 1f, 0.85f, 1f));
            badgeFill.sprite = GetUiSprite("UI_Anel");
            badgeFill.type = Image.Type.Filled;
            badgeFill.fillMethod = Image.FillMethod.Radial360;
            badgeFill.fillOrigin = (int)Image.Origin360.Top;
            badgeFill.fillAmount = 0f;

            AudioSource voice = root.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.25f; // Som claro e próximo nos dois ouvidos do Quest 2
            voice.volume = 1.0f;

            var ctrl = root.AddComponent<CetemMascotController>();
            SetSerializedField(ctrl, "mascotVisual", visual.transform);
            SetSerializedField(ctrl, "waveArm", arm);
            SetSerializedField(ctrl, "mouth", mouth);
            SetSerializedField(ctrl, "eyelidLeft", lidL);
            SetSerializedField(ctrl, "eyelidRight", lidR);
            SetSerializedField(ctrl, "speechBubbleRoot", bubble);
            SetSerializedField(ctrl, "speechTitleText", titleText);
            SetSerializedField(ctrl, "speechBodyText", bodyText);
            SetSerializedField(ctrl, "speechBubbleCanvasGroup", group);
            SetSerializedField(ctrl, "startBadgeRoot", startBadge);
            SetSerializedField(ctrl, "startBadgeLabel", badgeLabel);
            SetSerializedField(ctrl, "startBadgeRadialFill", badgeFill);
            SetSerializedField(ctrl, "voiceSource", voice);
            SetSerializedField(ctrl, "introVoiceClip", introClip);
            SetSerializedField(ctrl, "outroVoiceClip", outroClip);
            return ctrl;
        }

        private static GameObject AddSprite(Transform parent, string name, Sprite sprite, Vector3 localPos, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go;
        }

        // =========================================================================
        // UI
        // =========================================================================

        private static GameObject CreateBannerPanel(string name, Transform parent, string title, string message)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(0f, 2.45f, 3.55f);
            panel.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

            Canvas canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1640f, 360f);
            rt.localScale = Vector3.one * 0.00145f;

            UIImage(panel.transform, "Borda", new Vector2(1652f, 372f), Vector2.zero, new Color(0f, 0.85f, 1f, 0.9f));
            UIImage(panel.transform, "Fundo", new Vector2(1640f, 360f), Vector2.zero, new Color(0.02f, 0.04f, 0.09f, 0.96f));
            UIText(panel.transform, "Titulo", title, 78, FontStyle.Bold, new Color(0.35f, 0.95f, 1f), TextAnchor.MiddleCenter, new Vector2(1560f, 110f), new Vector2(0f, 88f));
            UIText(panel.transform, "Mensagem", message, 48, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(1560f, 180f), new Vector2(0f, -68f));
            return panel;
        }

        private static Image CreateGazeReticle(Transform cam)
        {
            GameObject go = new GameObject("Reticula_Olhar");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.8f);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);
            rt.localScale = Vector3.one * 0.0005f;

            Image dot = UIImage(go.transform, "Ponto", new Vector2(14f, 14f), Vector2.zero, new Color(1f, 1f, 1f, 0.8f));
            dot.sprite = GetUiSprite("UI_Circulo");

            Image fill = UIImage(go.transform, "Anel", new Vector2(56f, 56f), Vector2.zero, Color.cyan);
            fill.sprite = GetUiSprite("UI_Anel");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillAmount = 0f;
            return fill;
        }

        private static void CreateControllerPointer(string name, UnityEngine.XR.XRNode node, Transform parent)
        {
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = name;
            tip.transform.SetParent(parent, false);
            tip.transform.localScale = Vector3.one * 0.035f;
            tip.GetComponent<Renderer>().sharedMaterial = CetemMaterialLibrary.Glow("Ponteiro", new Color(0.3f, 0.9f, 1f), 1.5f);

            var rb = tip.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var lr = tip.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.004f;
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var hand = tip.AddComponent<HandInteractionTip>();
            SetSerializedField(hand, "handNode", node);
            SetSerializedField(hand, "pointerLineRenderer", lr);
        }

        private static Image UIImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        private static Text UIText(Transform parent, string name, string content, int size, FontStyle style, Color color, TextAnchor anchor, Vector2 rectSize, Vector2 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.sizeDelta = rectSize;
            rt.anchoredPosition = pos;
            return t;
        }

        private static Sprite GetUiSprite(string name)
        {
            string path = $"{TexturesPath}/{name}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = name == "UI_Anel"
                        ? Mathf.Clamp01((1f - d) * 40f) * Mathf.Clamp01((d - 0.72f) * 40f)
                        : Mathf.Clamp01((1f - d) * 40f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // =========================================================================
        // MALHA DO CRISTAL
        // =========================================================================

        private static Mesh GetOrCreateCrystalMesh()
        {
            string path = $"{MeshesPath}/Cristal_Hexagonal.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const int sides = 6;
            const float radius = 0.045f;
            const float half = 0.04f;
            const float tip = 0.045f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            Vector3 top = new Vector3(0f, half + tip, 0f);
            Vector3 bottom = new Vector3(0f, -half - tip * 0.8f, 0f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 up = Vector3.up * half;

                AddQuad(verts, tris, p0 - up, p1 - up, p1 + up, p0 + up);
                AddTri(verts, tris, p0 + up, p1 + up, top);
                AddTri(verts, tris, p1 - up, p0 - up, bottom);
            }

            Mesh mesh = new Mesh { name = "Cristal_Hexagonal" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
        }

        private static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(v, t, a, b, c);
            AddTri(v, t, a, c, d);
        }

        // =========================================================================
        // UTILITÁRIOS
        // =========================================================================

        private static Sprite GetRealMineralSprite(string fileName)
        {
            string path = $"{TexturesPath}/{fileName}";
            if (!File.Exists(path)) return null;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (Mathf.Abs(importer.spritePixelsPerUnit - 1650f) > 1f) { importer.spritePixelsPerUnit = 1650f; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (changed) importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            return ProceduralTechHolograms.Part(type, name, parent, localPos, localScale, mat);
        }

        /// <summary>
        /// Garante apenas as configurações de Sprite sem alterar nenhum pixel da imagem já sem fundo enviada pelo usuário.
        /// </summary>
        private static void PrepareMascotSourceImporter()
        {
            if (!File.Exists(MascotTexturePath)) return;

            var importer = AssetImporter.GetAtPath(MascotTexturePath) as TextureImporter;
            if (importer != null && (!importer.isReadable || importer.textureType != TextureImporterType.Sprite || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureVoiceClips()
        {
            string[] files = { "vo_intro.mp3", "vo_neodimio.mp3", "vo_litio.mp3", "vo_niobio.mp3", "vo_silicio.mp3", "vo_outro.mp3" };
            foreach (string f in files)
            {
                string p = $"{AudioPath}/{f}";
                if (!File.Exists(p)) continue;
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(p) as AudioImporter;
                if (importer != null)
                {
                    var sampleSettings = importer.defaultSampleSettings;
                    sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                    sampleSettings.compressionFormat = AudioCompressionFormat.PCM;
                    importer.defaultSampleSettings = sampleSettings;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Material CreateHoloMaterial(string name, Shader shader, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_AlphaMin")) mat.SetFloat("_AlphaMin", 0.08f);
            if (mat.HasProperty("_RimIntensity")) mat.SetFloat("_RimIntensity", 1.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static MineralData CreateOrUpdateMineral(string fileName, string name, string symbol, string category, string desc, Color color, AudioClip voiceClip)
        {
            string path = $"{ScriptableObjectsPath}/{fileName}.asset";
            MineralData data = AssetDatabase.LoadAssetAtPath<MineralData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MineralData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.mineralName = name;
            data.chemicalSymbol = symbol;
            data.categorySubtitle = category;
            data.shortDescription = desc;
            data.highlightColor = color;
            data.mascotExplanationClip = voiceClip;
            data.applicationHologramPrefab = null;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetSerializedField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[CETEM VR] Campo '{fieldName}' não encontrado em {target.GetType().Name}");
                return;
            }

            if (value == null)
            {
                prop.objectReferenceValue = null;
            }
            else if (value is Object uObj)
            {
                prop.objectReferenceValue = uObj;
            }
            else if (value is System.Collections.IList list)
            {
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i] as Object;
                }
            }
            else if (value is System.Enum e)
            {
                prop.intValue = System.Convert.ToInt32(e);
            }
            so.ApplyModifiedProperties();
        }

        private static void EnsureDirectories()
        {
            EnsureDirectory(BasePath);
            EnsureDirectory(ScriptableObjectsPath);
            EnsureDirectory(MaterialsPath);
            EnsureDirectory(PrefabsPath);
            EnsureDirectory(ScenesPath);
            EnsureDirectory(TexturesPath);
            EnsureDirectory(MeshesPath);
            EnsureDirectory(AudioPath);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return;
            }
            var list = new List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(scenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
