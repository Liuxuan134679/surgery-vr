using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShuQi.Editor
{
    [InitializeOnLoad]
    public static class LearningHallBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/学习大厅.unity";
        const string Imported = "Assets/_Project/art/VRProjects";
        const string Art = "Assets/_Project/art/LearningHall";
        const string Request = "Temp/ShuqiLearningHall.request";
        const string Reports = "D:/CGJ/outputs/shuqi_learning_hall";
        static readonly string[] ModelNames = { "SM_OPR_Table_02", "SM_OPR_Table_03", "SM_OPR_Cabinet_01_Full", "SM_OPR_Tray_01", "SM_OPR_Scalpel_01", "SM_OPR_Forceps_01", "SM_OPR_Surgical_Scissors_01", "SM_OPR_Scalpel_Handle_01", "SM_OPR_Thumb_Tissue_Forceps_01" };
        static Font font;
        static Material wall, floor, trim, teal, ink, pale, white, metal, amber;

        static LearningHallBuilder() { EditorApplication.delayCall += RunPending; }

        static void RunPending()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunPending;
                return;
            }
            string action = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                PrepareImportedMaterials();
                InspectModels();
                if (action == "build") Build();
                File.WriteAllText(Reports + "/editor-result.txt", "PASS: " + action + "\n" + DateTime.Now.ToString("s"));
                Debug.Log("SHUQI_LEARNING_HALL_PASS " + action);
            }
            catch (Exception error)
            {
                File.WriteAllText(Reports + "/editor-result.txt", "FAIL\n" + error);
                Debug.LogException(error);
            }
        }

        static void PrepareImportedMaterials()
        {
            foreach (string id in AssetDatabase.FindAssets("t:Texture2D", new[] { Imported }))
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool normal = path.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0;
                bool packed = path.IndexOf("OcclusionRoughnessMetallic", StringComparison.OrdinalIgnoreCase) >= 0;
                bool changed = importer.maxTextureSize != 1024 || importer.isReadable ||
                    (normal && importer.textureType != TextureImporterType.NormalMap) ||
                    (packed && importer.sRGBTexture);
                if (!changed) continue;
                importer.maxTextureSize = 1024;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                if (normal) importer.textureType = TextureImporterType.NormalMap;
                if (packed) importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            foreach (string id in AssetDatabase.FindAssets("t:Material", new[] { Imported }))
            {
                string materialPath=AssetDatabase.GUIDToAssetPath(id);
                if(!materialPath.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                material.shader = Shader.Find("Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", material.name.Contains("Surgical_Set") ? 0.65f : 0.18f);
                material.SetFloat("_Smoothness", material.name.Contains("Glass") ? 0.8f : 0.35f);
                if (material.GetTexture("_BumpMap")) material.EnableKeyword("_NORMALMAP");
                if (material.GetTexture("_OcclusionMap")) material.EnableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_OcclusionStrength", 0.35f);
                material.enableInstancing = true;
                if (material.name.Contains("Glass"))
                {
                    material.SetColor("_BaseColor", new Color(0.75f, 0.87f, 0.88f, 0.28f));
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Imported+"/Fonts/NotoSansSC-Regular.otf",ImportAssetOptions.ForceUpdate);
        }

        static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + obj.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void InspectModels()
        {
            Directory.CreateDirectory(Reports);
            var report = new List<string>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
            foreach (string name in ModelNames)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Imported + "/Prefabs/" + name + ".prefab");
                if (!prefab) throw new InvalidOperationException("Missing imported prefab: " + name);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.transform.position = Vector3.zero;
                var bounds = BoundsOf(instance);
                long triangles = instance.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh ? Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i => (long)m.sharedMesh.GetIndexCount(i)/3) : 0);
                report.Add(name + " | size " + bounds.size.ToString("F4") + " | center " + bounds.center.ToString("F4") + " | triangles with all LODs " + triangles);
                Object.DestroyImmediate(instance);
            }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllLines(Reports + "/model-inspection.txt", report);
        }

        static void Build()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Require(!scene.isDirty, "学习大厅有未保存修改，请先保存或另存后再搭建。");
            SceneManager.SetActiveScene(scene);
            var existing = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "LearningHallRoot");
            if (existing) Object.DestroyImmediate(existing);
            var diagnostics = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "LegacyDiagnostics_Preserved");
            if (!diagnostics)
            {
                diagnostics = new GameObject("LegacyDiagnostics_Preserved");
                var oldNames = new[] { "Floor", "WorkTable", "TableBase", "LeftTriggerIndicator", "RightTriggerIndicator", "Diagnostics", "Environment Status", "Environment Probe" };
                foreach (var obj in scene.GetRootGameObjects().Where(o => oldNames.Contains(o.name)).ToArray())
                    obj.transform.SetParent(diagnostics.transform, true);
                diagnostics.SetActive(false);
            }

            Directory.CreateDirectory(Art + "/Materials");
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<Font>(Imported + "/Fonts/NotoSansSC-Regular.otf");
            Require(font, "Missing imported Chinese font");
            wall = Mat("Wall_Ivory", new Color(.89f,.93f,.92f));
            floor = Mat("Floor_SoftGrey", new Color(.57f,.65f,.66f));
            trim = Mat("Trim_Slate", new Color(.11f,.23f,.27f));
            teal = Mat("Accent_Teal", new Color(.04f,.46f,.48f));
            ink = Mat("Ink_Navy", new Color(.035f,.095f,.13f));
            pale = Mat("Pale_Teal", new Color(.69f,.85f,.83f));
            white = Mat("White", new Color(.95f,.97f,.96f));
            metal = Mat("Metal", new Color(.45f,.55f,.57f), .5f);
            amber = Mat("Amber", new Color(.83f,.49f,.18f));
            var root = new GameObject("LearningHallRoot");
            var shell = Group(root.transform, "01_RoomShell_PrimitivePlaceholder");
            Box(shell, "Floor", new Vector3(0,-.08f,.2f), new Vector3(6.4f,.16f,6.4f), floor);
            Box(shell, "NorthWall", new Vector3(0,1.55f,3.3f), new Vector3(6.4f,3.1f,.16f), wall);
            Box(shell, "WestWall", new Vector3(-3.2f,1.55f,.2f), new Vector3(.16f,3.1f,6.4f), wall);
            Box(shell, "EastWall", new Vector3(3.2f,1.55f,.2f), new Vector3(.16f,3.1f,6.4f), wall);
            Box(shell, "SouthWall_Left", new Vector3(-.85f,1.55f,-3f), new Vector3(4.7f,3.1f,.16f), wall);
            Box(shell, "SouthWall_Right", new Vector3(2.97f,1.55f,-3f), new Vector3(.45f,3.1f,.16f), wall);
            Box(shell, "DoorLintel", new Vector3(2.14f,2.72f,-3f), new Vector3(1.2f,.76f,.16f), wall);
            Box(shell, "Door_PrimitivePlaceholder", new Vector3(2.14f,1.1f,-3.02f), new Vector3(1.12f,2.2f,.08f), trim);
            Box(shell, "DoorWindow", new Vector3(2.14f,1.56f,-2.97f), new Vector3(.64f,.57f,.025f), pale, false);
            Box(shell, "DoorHandle", new Vector3(1.74f,1.0f,-2.94f), new Vector3(.035f,.24f,.065f), metal, false);
            Box(shell, "NorthSkirting", new Vector3(0,.09f,3.19f), new Vector3(6.24f,.18f,.035f), trim, false);
            Box(shell, "WestSkirting", new Vector3(-3.09f,.09f,.2f), new Vector3(.035f,.18f,6.24f), trim, false);
            Box(shell, "EastSkirting", new Vector3(3.09f,.09f,.2f), new Vector3(.035f,.18f,6.24f), trim, false);
            Box(shell, "NorthAccentBand", new Vector3(0,2.55f,3.19f), new Vector3(6.24f,.09f,.025f), teal, false);
            Box(shell, "WestAccentBand", new Vector3(-3.09f,2.55f,.2f), new Vector3(.025f,.09f,6.24f), teal, false);
            Box(shell, "EastAccentBand", new Vector3(3.09f,2.55f,.2f), new Vector3(.025f,.09f,6.24f), teal, false);
            for (int i=-2;i<=2;i++)
            {
                Box(shell, "FloorJoint_X_"+i, new Vector3(i,.002f,.2f), new Vector3(.008f,.003f,6.2f), pale, false);
                Box(shell, "FloorJoint_Z_"+i, new Vector3(0,.002f,i+.2f), new Vector3(6.2f,.003f,.008f), pale, false);
            }
            var ceiling = Group(shell, "Ceiling_CutawayForPreview");
            var roof=Box(ceiling, "Ceiling", new Vector3(0,3.15f,.2f), new Vector3(6.4f,.1f,6.4f), wall);
            roof.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Box(ceiling, "LightPanel_Front", new Vector3(-1,3.085f,1.4f), new Vector3(1.1f,.025f,.45f), white, false);
            Box(ceiling, "LightPanel_Back", new Vector3(1,3.085f,-1.2f), new Vector3(1.1f,.025f,.45f), white, false);

            var station = Group(root.transform, "02_LearningStation");
            var bench = Group(station, "C_TrainingBench_ImportedModels");
            var leftTable = Place("SM_OPR_Table_02", bench, new Vector3(-.48f,0,.65f), 90,1);
            var rightTable = Place("SM_OPR_Table_02", bench, new Vector3(.48f,0,.65f), 90,1);
            float top = Mathf.Max(BoundsOf(leftTable).max.y,BoundsOf(rightTable).max.y);
            Box(bench, "TrainingMat", new Vector3(0,top+.007f,.65f), new Vector3(1.84f,.012f,.55f), trim);
            var tools = Group(bench, "TutorialTools_StaticLayout");
            Place("SM_OPR_Tray_01", tools, new Vector3(-.64f,top+.015f,.67f),0,1);
            var knife = Group(tools, "Scalpel_Assembly_Layout");
            Place("SM_OPR_Scalpel_Handle_01", knife, new Vector3(-.27f,top+.022f,.65f),90,1);
            Place("SM_OPR_Scalpel_01", knife, new Vector3(-.27f,top+.022f,.735f),90,1);
            Place("SM_OPR_Thumb_Tissue_Forceps_01", tools, new Vector3(.18f,top+.022f,.65f),90,1);
            Place("SM_OPR_Surgical_Scissors_01", tools, new Vector3(.62f,top+.022f,.65f),90,1);
            var toolLabels = CanvasAt("ToolLabels", bench, new Vector3(0,top-.025f,.327f), new Vector2(1.84f,.085f));
            TextAt(toolLabels, "TrayLabel", "托盘", new Vector2(-650,0), new Vector2(300,70),40,Color.white);
            TextAt(toolLabels, "ScalpelLabel", "手术刀", new Vector2(-270,0), new Vector2(300,70),40,Color.white);
            TextAt(toolLabels, "ForcepsLabel", "镊子", new Vector2(180,0), new Vector2(300,70),40,Color.white);
            TextAt(toolLabels, "ScissorsLabel", "剪刀", new Vector2(620,0), new Vector2(300,70),40,Color.white);

            var ui = Group(station, "A_WorldSpaceUI_LayoutPlaceholder");
            Box(ui, "PanelFrame", new Vector3(0,1.88f,1.58f),new Vector3(2.28f,1.17f,.08f),trim,false);
            var main = CanvasAt("EntryAndReviewCanvas", ui,new Vector3(0,1.88f,1.532f),new Vector2(2.18f,1.07f));
            ImageAt(main,"PanelBackground",Vector2.zero,new Vector2(2180,1070),new Color(.035f,.095f,.13f));
            TextAt(main,"Brand","术启  /  SURGICAL LEARNING HUB",new Vector2(-10,410),new Vector2(2000,105),60,Color.white,TextAnchor.MiddleLeft);
            TextAt(main,"Title","从这里，开启第一台手术",new Vector2(-10,225),new Vector2(2000,140),88,Color.white,TextAnchor.MiddleLeft);
            TextAt(main,"Subtitle","上背部浅表脂肪瘤切除术 · 新手全过程训练",new Vector2(-10,90),new Vector2(2000,90),45,new Color(.63f,.84f,.83f),TextAnchor.MiddleLeft);
            string[] modes={"引导学习","自主练习","操作复盘"};
            string[] descriptions={"跟随提示，认识每个步骤","熟悉流程，练习双手配合","回看操作，找到改进方向"};
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*670;
                ImageAt(main,"ModeCard_"+i,new Vector2(x,-170),new Vector2(630,240),i==0?new Color(.04f,.46f,.48f):new Color(.10f,.21f,.25f));
                TextAt(main,"Mode_"+i,modes[i],new Vector2(x,-135),new Vector2(590,100),64,Color.white);
                TextAt(main,"ModeDescription_"+i,descriptions[i],new Vector2(x,-235),new Vector2(590,65),32,new Color(.78f,.88f,.88f));
            }
            TextAt(main,"Status","S01 认识目标   →   S02 调整站位   →   S03 练习器械   →   S04 选择病例",new Vector2(0,-430),new Vector2(2020,80),36,new Color(.66f,.81f,.82f));

            var preview = Group(station, "CasePreviewBoard_PrimitivePlaceholder");
            Box(preview,"Base",new Vector3(-2.0f,.035f,.85f),new Vector3(.65f,.07f,.5f),trim);
            Box(preview,"Stand",new Vector3(-2.0f,.65f,.85f),new Vector3(.06f,1.23f,.06f),metal);
            Box(preview,"Frame",new Vector3(-2.0f,1.68f,.85f),new Vector3(1.0f,1.4f,.075f),trim);
            var caseUI=CanvasAt("CasePreviewCanvas",preview,new Vector3(-2.0f,1.68f,.804f),new Vector2(.92f,1.32f));
            ImageAt(caseUI,"CaseBackground",Vector2.zero,new Vector2(920,1320),new Color(.91f,.95f,.94f));
            TextAt(caseUI,"CaseCode","标准病例  /  CASE 01",new Vector2(0,520),new Vector2(820,110),55,new Color(.04f,.35f,.37f));
            ImageAt(caseUI,"BodyHead",new Vector2(-220,245),new Vector2(120,120),new Color(.49f,.64f,.66f));
            ImageAt(caseUI,"BodyTorso",new Vector2(-220,-10),new Vector2(270,350),new Color(.49f,.64f,.66f));
            ImageAt(caseUI,"UpperBackMarker",new Vector2(-220,100),new Vector2(150,85),new Color(.03f,.47f,.49f));
            TextAt(caseUI,"BodyLabel","上背部\n浅表肿物",new Vector2(175,30),new Vector2(330,230),57,new Color(.07f,.20f,.24f));
            TextAt(caseUI,"CaseSummary","成人 · 单发 · 局麻病例\n\n认识目标与部位\n学习术前到术后的完整流程",new Vector2(0,-370),new Vector2(800,310),47,new Color(.12f,.26f,.29f));

            var equipment = Group(root.transform,"03_SupportArea");
            Place("SM_OPR_Cabinet_01_Full",equipment,new Vector3(2.12f,0,2.05f),90,1);
            var cart=Place("SM_OPR_Table_03",equipment,new Vector3(2.12f,0,-.55f),0,1);
            float cartTop=BoundsOf(cart).max.y;
            var recovery=Group(equipment,"RecoveryContainers_PrimitivePlaceholder");
            Box(recovery,"SharpsContainer",new Vector3(1.98f,cartTop+.13f,-.55f),new Vector3(.23f,.26f,.24f),amber);
            Box(recovery,"SharpsLid",new Vector3(1.98f,cartTop+.272f,-.55f),new Vector3(.25f,.03f,.26f),trim,false);
            Box(recovery,"WasteContainer",new Vector3(2.30f,cartTop+.13f,-.55f),new Vector3(.23f,.26f,.24f),teal);
            var returnUI=CanvasAt("RecoveryLabels",recovery,new Vector3(2.14f,cartTop+.15f,-.686f),new Vector2(.60f,.16f));
            TextAt(returnUI,"SharpsLabel","锐器",new Vector2(-160,0),new Vector2(270,120),50,Color.white);
            TextAt(returnUI,"WasteLabel","回收",new Vector2(160,0),new Vector2(270,120),50,Color.white);

            var spawn = Group(root.transform,"B_CalibrationAndSpawn");
            Box(spawn,"CalibrationPatch",new Vector3(0,.006f,-.65f),new Vector3(1.25f,.008f,1.15f),pale,false);
            Box(spawn,"FrontEdge",new Vector3(0,.012f,-.08f),new Vector3(1.25f,.008f,.025f),teal,false);
            Box(spawn,"LeftEdge",new Vector3(-.62f,.012f,-.65f),new Vector3(.025f,.008f,1.15f),teal,false);
            Box(spawn,"RightEdge",new Vector3(.62f,.012f,-.65f),new Vector3(.025f,.008f,1.15f),teal,false);
            Box(spawn,"BackEdge",new Vector3(0,.012f,-1.22f),new Vector3(1.25f,.008f,.025f),teal,false);
            var floorUI=CanvasAt("CalibrationLabel",spawn,new Vector3(0,.018f,-.95f),new Vector2(1.05f,.18f));
            floorUI.rotation=Quaternion.Euler(90,0,0);
            TextAt(floorUI,"FloorText","站立校准区",Vector2.zero,new Vector2(1000,140),90,new Color(.04f,.29f,.32f));
            var marker=Group(spawn,"Spawn_Learning");marker.position=new Vector3(0,0,-.65f);
            var rig=scene.GetRootGameObjects().Single(o=>o.name=="QuestRig");
            rig.transform.position=marker.position;rig.transform.rotation=Quaternion.identity;
            var probe=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ShuQi.VR.QuestEnvironmentProbe>(true)).Single();
            probe.transform.SetParent(rig.transform,true);
            probe.gameObject.SetActive(true);
            probe.leftController.enabled=false;
            probe.rightController.enabled=false;
            var camera=rig.GetComponentInChildren<Camera>(true);camera.nearClipPlane=.04f;camera.farClipPlane=30;

            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.70f,.76f,.78f);
            RenderSettings.skybox=null;RenderSettings.fog=false;
            var sun=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun){sun.intensity=.75f;sun.color=new Color(1,.98f,.95f);sun.transform.rotation=Quaternion.Euler(42,-35,0);sun.shadows=LightShadows.None;}
            var fill=new GameObject("HallFillLight").AddComponent<Light>();fill.transform.SetParent(root.transform,false);fill.transform.position=new Vector3(0,2.6f,-.6f);fill.type=LightType.Point;fill.range=8;fill.intensity=1.1f;fill.shadows=LightShadows.None;
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save learning hall");
            var paths=new[]{ScenePath,"Assets/_Project/Scenes/小手术操作室.unity","Assets/_Project/Scenes/复诊室.unity"};
            EditorBuildSettings.scenes=paths.Where(File.Exists).Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            AssetDatabase.SaveAssets();
            Canvas.ForceUpdateCanvases();
            ValidateScene(scene);
            QuestEnvironmentSetup.Validate();
            ExportViews(scene,root,ceiling.gameObject);
            SceneVisibilityManager.instance.Hide(ceiling.gameObject,true);
            foreach(var t in shell.GetComponentsInChildren<Transform>())
                if(t.name.StartsWith("SouthWall_")||t.name.StartsWith("Door"))SceneVisibilityManager.instance.Hide(t.gameObject,true);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(new Vector3(0,1.2f,.6f),Quaternion.Euler(24,10,0),5.8f);
        }

        static Material Mat(string name,Color color,float metallic=0)
        {
            string path=Art+"/Materials/"+name+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!result){result=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(result,path);}
            result.SetColor("_BaseColor",color);result.SetFloat("_Metallic",metallic);result.SetFloat("_Smoothness",.25f);result.enableInstancing=true;EditorUtility.SetDirty(result);return result;
        }
        static Transform Group(Transform parent,string name){var obj=new GameObject(name);obj.transform.SetParent(parent,false);return obj.transform;}
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collider=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static GameObject Place(string name,Transform parent,Vector3 bottomCenter,float yaw,float scale)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"/Prefabs/"+name+".prefab");Require(prefab,"Missing model "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);obj.name=name;obj.transform.rotation=Quaternion.Euler(0,yaw,0);obj.transform.localScale=Vector3.one*scale;
            var b=BoundsOf(obj);obj.transform.position+=bottomCenter-new Vector3(b.center.x,b.min.y,b.center.z);
            if(name.Contains("Tray")||name.Contains("Scalpel")||name.Contains("Forceps")||name.Contains("Scissors"))
                foreach(var group in obj.GetComponentsInChildren<LODGroup>())
                {
                    var levels=group.GetLODs();
                    group.SetLODs(new[]{new LOD(0f,levels[0].renderers)});
                    group.RecalculateBounds();
                }
            return obj;
        }
        static Transform CanvasAt(string name,Transform parent,Vector3 position,Vector2 meters)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=Vector3.one*.001f;
            var rt=(RectTransform)go.transform;rt.sizeDelta=meters*1000;go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;return go.transform;
        }
        static void ImageAt(Transform canvas,string name,Vector2 pos,Vector2 size,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image));obj.transform.SetParent(canvas,false);var rt=(RectTransform)obj.transform;rt.anchoredPosition=pos;rt.sizeDelta=size;var image=obj.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        }
        static void TextAt(Transform canvas,string name,string value,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Text));obj.transform.SetParent(canvas,false);var rt=(RectTransform)obj.transform;rt.anchoredPosition=pos;rt.sizeDelta=size;var text=obj.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.text=value;text.color=color;text.alignment=align;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
        }
        static void ValidateScene(Scene scene)
        {
            var objects=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            Require(objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount)==0,"Missing scene scripts");
            Require(objects.SelectMany(o=>o.GetComponents<Camera>()).Count(c=>c.isActiveAndEnabled)==1,"Expected one active XR camera");
            Require(objects.SelectMany(o=>o.GetComponents<AudioListener>()).Count(c=>c.isActiveAndEnabled)==1,"Expected one active audio listener");
            var renderers=objects.Where(o=>o.activeInHierarchy).SelectMany(o=>o.GetComponents<Renderer>()).ToArray();
            Require(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader)),"Missing scene materials or shaders");
            Require(font.material.shader.name!="Universal Render Pipeline/Lit","Font material must retain its text shader");
            Require(objects.Any(o=>o.name=="Spawn_Learning")&&objects.Any(o=>o.name=="CasePreviewCanvas")&&objects.Any(o=>o.name=="EntryAndReviewCanvas"),"Hall zones are incomplete");
            File.WriteAllText(Reports+"/scene-validation.txt","PASS\nScene: "+ScenePath+"\nActive renderers: "+renderers.Length+"\nActive XR cameras: 1\nMissing scripts: 0\nImported prefab roots: "+objects.Count(o=>PrefabUtility.IsAnyPrefabInstanceRoot(o))+"\nScope: room and model layout; grabbing, UI actions and chapter logic not connected.\nQuest 3 hardware: NOT TESTED\n");
        }
        static void ExportViews(Scene scene,GameObject root,GameObject ceiling)
        {
            var cameraObj=new GameObject("HallPreviewCamera_EditorOnly");SceneManager.MoveGameObjectToScene(cameraObj,scene);var camera=cameraObj.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.78f,.84f,.84f);camera.nearClipPlane=.03f;camera.farClipPlane=40;camera.enabled=false;
            try
            {
                RenderView(camera,new Vector3(0,1.65f,-.65f),new Vector3(0,1.55f,1.4f),false,"hall-player-view.png");
                RenderView(camera,new Vector3(-2.75f,2.3f,-2.45f),new Vector3(.25f,1.15f,1),false,"hall-overview.png");
                ceiling.SetActive(false);
                RenderView(camera,new Vector3(0,10,.2f),new Vector3(0,0,.201f),true,"hall-top-view.png");
            }
            finally{ceiling.SetActive(true);Object.DestroyImmediate(cameraObj);}
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static void RenderView(Camera camera,Vector3 position,Vector3 target,bool orthographic,string file)
        {
            camera.transform.position=position;camera.transform.LookAt(target);camera.orthographic=orthographic;camera.orthographicSize=3.7f;camera.fieldOfView=65;
            var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1600,1100,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1100),0,0);texture.Apply();File.WriteAllBytes(Reports+"/"+file,texture.EncodeToPNG());Object.DestroyImmediate(texture);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
