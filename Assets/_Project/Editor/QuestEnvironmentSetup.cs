using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Unity.XR.CoreUtils;
using ShuQi.VR;
using Object = UnityEngine.Object;

namespace ShuQi.Editor
{
    public static class QuestEnvironmentSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/QuestEnvironment.unity";
        const string SettingsRoot = "Assets/_Project/Settings";
        const string PipelinePath = SettingsRoot + "/QuestURP.asset";
        const string ReportRoot = "Builds/Reports";

        [MenuItem("ShuQi/Environment/Configure Quest 3")]
        public static void Configure()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { SettingsRoot, "Assets/_Project/Scenes", "Assets/_Project/Materials", "Assets/_Project/Prefabs", "Assets/XR/Settings" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            ConfigurePlayer();
            ConfigureRendering();
            ConfigureXR(BuildTargetGroup.Android);
            ConfigureXR(BuildTargetGroup.Standalone);
            if (!File.Exists(ScenePath)) CreateValidationScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("SHUQI_ENVIRONMENT_CONFIGURED");
        }

        static void ConfigurePlayer()
        {
            string android = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
            AndroidExternalToolsSettings.sdkRootPath = Path.Combine(android, "SDK");
            AndroidExternalToolsSettings.ndkRootPath = Path.Combine(android, "NDK");
            AndroidExternalToolsSettings.jdkRootPath = Path.Combine(android, "OpenJDK");
            PlayerSettings.companyName = "ShuQi";
            PlayerSettings.productName = "术启 VR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.shuqi.surgeryvr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            player.FindProperty("activeInputHandler").intValue = 1;
            player.ApplyModifiedPropertiesWithoutUndo();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
        }

        static void ConfigureRendering()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, SettingsRoot + "/QuestRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsRoot + "/QuestRenderer.asset");
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(rendererData);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 12f;
            pipeline.shadowCascadeCount = 1;
            var pipelineData = new SerializedObject(pipeline);
            pipelineData.FindProperty("m_MainLightShadowmapResolution").intValue = 1024;
            pipelineData.FindProperty("m_SoftShadowsSupported").boolValue = false;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.SetQualityLevel(2, true);
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var overridePipeline = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                if (overridePipeline != null) overridePipeline.objectReferenceValue = pipeline;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        static void ConfigureXR(BuildTargetGroup group)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget all))
            {
                all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(all, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, all, true);
            }
            if (!all.HasManagerSettingsForBuildTarget(group)) all.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = all.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            general.Manager.automaticLoading = true;
            general.Manager.automaticRunning = true;
            Require(XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, group), "Could not assign OpenXR loader for " + group);

            if (OpenXRSettings.GetSettingsForBuildTargetGroup(group) == null)
            {
                var package = XRPackageMetadataStore.GetAllPackageMetadata().First(p => p.metadata.packageId == "com.unity.xr.openxr");
                var container = ScriptableObject.CreateInstance(package.metadata.settingsType);
                AssetDatabase.CreateAsset(container, "Assets/XR/Settings/OpenXR Package Settings.asset");
                Require(package.PopulateNewSettingsInstance(container), "Could not register OpenXR settings");
            }
            FeatureHelpers.RefreshFeatures(group);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            foreach (var feature in settings.GetFeatures()) feature.enabled = false;
            Enable<OculusTouchControllerProfile>(settings);
            Enable<MetaQuestTouchPlusControllerProfile>(settings);
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            if (group == BuildTargetGroup.Android)
            {
                var quest = Enable<MetaQuestFeature>(settings);
                var data = new SerializedObject(quest);
                var devices = data.FindProperty("targetDevices");
                for (int i = 0; i < devices.arraySize; i++)
                {
                    var device = devices.GetArrayElementAtIndex(i);
                    device.FindPropertyRelative("enabled").boolValue = device.FindPropertyRelative("manifestName").stringValue == "eureka";
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(quest);
            }
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(all);
            EditorUtility.SetDirty(settings);
        }

        static T Enable<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            Require(feature != null, "OpenXR feature was not discovered: " + typeof(T).Name);
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            return feature;
        }

        static Material Material(string name, Color color)
        {
            string path = "Assets/_Project/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                result.color = color;
                AssetDatabase.CreateAsset(result, path);
            }
            return result;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static void AddPose(Transform target, string device, bool head = false)
        {
            var driver = target.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, device + (head ? "/centerEyePosition" : "/devicePosition"), expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, device + (head ? "/centerEyeRotation" : "/deviceRotation"), expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("TrackingState", InputActionType.Value, device + "/trackingState", expectedControlType: "Integer"));
        }

        static void CreateValidationScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.53f, 0.58f);
            var floor = Material("Floor", new Color(0.19f, 0.25f, 0.28f));
            var table = Material("Table", new Color(0.65f, 0.72f, 0.73f));
            var left = Material("LeftCyan", new Color(0.12f, 0.68f, 0.80f));
            var right = Material("RightOrange", new Color(0.95f, 0.47f, 0.16f));
            Box("Floor", new Vector3(0,-0.05f,1), new Vector3(6,0.1f,6), floor);
            Box("WorkTable", new Vector3(0,0.85f,0.85f), new Vector3(1.5f,0.1f,0.7f), table);
            Box("TableBase", new Vector3(0,0.4f,0.85f), new Vector3(0.8f,0.8f,0.4f), table);
            var leftCube = Box("LeftTriggerIndicator", new Vector3(-0.28f,1,0.8f), Vector3.one*0.14f, left);
            var rightCube = Box("RightTriggerIndicator", new Vector3(0.28f,1,0.8f), Vector3.one*0.14f, right);
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Hard;
            light.transform.rotation = Quaternion.Euler(45,-30,0);

            var rig = new GameObject("QuestRig");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.SetParent(offset.transform, false);
            var cam = camera.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f,0.12f,0.16f);
            cam.allowHDR = false;
            camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            AddPose(camera.transform, "<XRHMD>", true);
            var origin = rig.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 1.65f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            offset.transform.localPosition = new Vector3(0,1.65f,0);
            var leftHand = Box("Left Controller", Vector3.zero, new Vector3(0.055f,0.09f,0.10f), left);
            var rightHand = Box("Right Controller", Vector3.zero, new Vector3(0.055f,0.09f,0.10f), right);
            foreach (var hand in new[] { leftHand, rightHand })
            {
                Object.DestroyImmediate(hand.GetComponent<Collider>());
                hand.transform.SetParent(offset.transform, false);
            }
            AddPose(leftHand.transform, "<XRController>{LeftHand}");
            AddPose(rightHand.transform, "<XRController>{RightHand}");
            PrefabUtility.SaveAsPrefabAsset(rig, "Assets/_Project/Prefabs/QuestRig.prefab");

            var board = new GameObject("Environment Status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            board.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = board.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1400,650);
            rect.position = new Vector3(0,1.73f,1.65f);
            rect.localScale = Vector3.one * 0.001f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(board.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;
            panel.GetComponent<Image>().color = new Color(0.035f,0.07f,0.10f,0.96f);
            var label = new GameObject("Diagnostics", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            label.GetComponent<RectTransform>().sizeDelta = new Vector2(1320,580);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = "SHUQI / QUEST 3 ENVIRONMENT CHECK\n\nPress Play to view tracking and controller input.\n\nLeft: cyan   Right: orange\nTrigger: color   X/A: haptics   Right B: pause";
            var probe = new GameObject("Environment Probe").AddComponent<QuestEnvironmentProbe>();
            probe.statusText = text;
            probe.leftController = leftHand.GetComponent<Renderer>();
            probe.rightController = rightHand.GetComponent<Renderer>();
            probe.leftTestObject = leftCube.GetComponent<Renderer>();
            probe.rightTestObject = rightCube.GetComponent<Renderer>();
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save validation scene");
        }

        [MenuItem("ShuQi/Environment/Validate Settings")]
        public static void Validate()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (var issue in issues) Debug.Log((issue.error ? "XR ERROR: " : "XR NOTE: ") + issue.message);
            Require(!issues.Any(issue => issue.error), "OpenXR project validation has blocking errors");
            Require(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "IL2CPP is required");
            Require(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "ARM64 is required");
            Require(PlayerSettings.colorSpace == ColorSpace.Linear, "Linear color space is required");
            Require(GraphicsSettings.defaultRenderPipeline == AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath), "Quest render pipeline is not active");
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsRoot + "/QuestRenderer.asset");
            var rendererProperties = new SerializedObject(rendererData);
            Require(rendererProperties.FindProperty("xrSystemData").objectReferenceValue != null, "URP XR shader resources are missing");
            Require(rendererProperties.FindProperty("shaders.blitPS").objectReferenceValue != null, "URP blit shader is missing");
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                Require(general && general.InitManagerOnStart && general.Manager.activeLoaders.OfType<OpenXRLoader>().Any(), "OpenXR loader missing for " + group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                Require(settings.GetFeature<OculusTouchControllerProfile>().enabled, "Oculus Touch profile missing");
                Require(settings.GetFeature<MetaQuestTouchPlusControllerProfile>().enabled, "Touch Plus profile missing");
                Require(settings.renderMode == OpenXRSettings.RenderMode.SinglePassInstanced, "Single-pass rendering is required");
            }
            Require(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaQuestFeature>().enabled, "Meta Quest Support is disabled");
            Require(Directory.Exists(AndroidExternalToolsSettings.sdkRootPath) && Directory.Exists(AndroidExternalToolsSettings.ndkRootPath) && Directory.Exists(AndroidExternalToolsSettings.jdkRootPath), "Android toolchain path missing");
            bool sceneLoadError = false;
            Application.LogCallback onLog = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) sceneLoadError = true; };
            Application.logMessageReceived += onLog;
            bool alreadyLoaded = SceneManager.GetSceneByPath(ScenePath).isLoaded;
            var scene = alreadyLoaded ? SceneManager.GetSceneByPath(ScenePath) : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                Require(objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount) == 0, "Scene has missing scripts");
                Require(objects.SelectMany(obj => obj.GetComponents<Camera>()).Count() == 1, "Expected one XR camera");
                Require(objects.SelectMany(obj => obj.GetComponents<AudioListener>()).Count() == 1, "Expected one audio listener");
                Require(objects.SelectMany(obj => obj.GetComponents<TrackedPoseDriver>()).Count() == 3, "Head and both controller pose drivers are required");
                Require(objects.SelectMany(obj => obj.GetComponents<Renderer>()).All(renderer => renderer.sharedMaterials.All(material => material && material.shader)), "Scene has missing materials/shaders");
                var probe = objects.SelectMany(obj => obj.GetComponents<QuestEnvironmentProbe>()).Single();
                Require(probe.statusText && probe.leftController && probe.rightController && probe.leftTestObject && probe.rightTestObject, "Diagnostics references are incomplete");
            }
            finally
            {
                if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
                Application.logMessageReceived -= onLog;
            }
            Require(!sceneLoadError, "Scene emitted an error during validation");
            Directory.CreateDirectory(ReportRoot);
            File.WriteAllText(ReportRoot + "/configuration-validation.txt", "PASS\nUnity: " + Application.unityVersion + "\nScene: " + ScenePath + "\nBackend: IL2CPP / ARM64\nGraphics: Vulkan / URP / Linear / 4x MSAA\nSDK: " + AndroidExternalToolsSettings.sdkRootPath + "\nHeadset hardware: NOT TESTED\n" + string.Join("\n", issues.Select(i => i.message)));
            Debug.Log("SHUQI_ENVIRONMENT_VALIDATION_PASS");
        }

        [MenuItem("ShuQi/Environment/Build Quest 3 APK")]
        public static void BuildAndroid()
        {
            Validate();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Android/ShuQiVR-Quest3-Development.apk", target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText(ReportRoot + "/android-build.txt", "Result: " + report.summary.result + "\nBytes: " + report.summary.totalSize + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nDuration: " + report.summary.totalTime + "\n");
            Require(report.summary.result == BuildResult.Succeeded, "Android build failed; inspect Unity build log");
            Debug.Log("SHUQI_ANDROID_BUILD_PASS");
        }

        public static void OpenValidationScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        public static void ConfigureAndBuild()
        {
            Configure();
            BuildAndroid();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
