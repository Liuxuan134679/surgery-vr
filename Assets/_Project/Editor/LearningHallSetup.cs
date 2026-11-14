using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autohand;
using ShuQi.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ShuQi.Editor
{
    [InitializeOnLoad]
    public static class LearningHallSetup
    {
        static readonly string Work=Path.GetFullPath("Temp/LearningHallChecks");
        const string Request="Temp/LearningHallSetup.request";
        const string ScenePath="Assets/_Project/Scenes/学习大厅.unity";
        const string RigPath="Assets/_Project/Prefabs/QuestRig.prefab";
        static Font font;
        static readonly Color Ink=new Color(.035f,.095f,.13f), Teal=new Color(.035f,.46f,.48f), Light=new Color(.78f,.9f,.9f);
        static double ready=EditorApplication.timeSinceStartup+2;
        static LearningHallSetup() { Directory.CreateDirectory(Work); EditorApplication.update+=Dispatch; }
        static void Dispatch()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup<ready || !File.Exists(Request)) return;
            string command=File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                if(command=="apply") Apply();
                else if(command=="validate") Validate();
                else if(command=="playtest") { File.WriteAllText("Temp/HallRuntimeCheck.active","1"); EditorApplication.isPlaying=true; }
                else if(command=="views") ExportViews();
                else if(command=="polish") Polish();
                else if(command=="materials") FinishMaterials();
                else if(command=="fingertip") ConfigureFingertipPointer();
                else if(command=="rings") ConfigurePlacementRings();
                else throw new ArgumentException("Unknown hall request");
                File.WriteAllText(Work+"/command-result.txt","PASS "+command);
            }
            catch(Exception e) { File.WriteAllText(Work+"/command-result.txt","FAIL "+command+"\n"+e); Debug.LogException(e); }
        }
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static Transform Find(Scene scene,string name) => scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name);
        static GameObject Group(Transform parent,string name)
        { var o=new GameObject(name); o.transform.SetParent(parent,false); return o; }
        static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var o=new GameObject(name,typeof(RectTransform)); o.transform.SetParent(parent,false);
            var rect=o.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=pos; rect.sizeDelta=size; o.layer=5; return rect;
        }
        static Text Label(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize=40,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var r=Rect(parent,name,pos,size); var t=r.gameObject.AddComponent<Text>(); t.font=font; t.fontSize=fontSize; t.color=Color.white; t.alignment=alignment; t.text=value;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Overflow; t.raycastTarget=false; return t;
        }
        static Button Button(Transform parent,string name,string value,Vector2 pos,Vector2 size)
        {
            var rect=Rect(parent,name,pos,size); var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.11f,.23f,.28f); image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var c=button.colors; c.highlightedColor=new Color(.75f,1,1); c.pressedColor=new Color(.6f,.8f,.8f); c.selectedColor=Color.white; c.disabledColor=new Color(.4f,.45f,.45f); button.colors=c;
            var nav=button.navigation; nav.mode=Navigation.Mode.None; button.navigation=nav;
            Label(rect,"Label",value,Vector2.zero,size-new Vector2(30,12),size.y<90?30:43); return button;
        }
        static GameObject Page(Transform parent,string name)
        { var rect=Rect(parent,name,Vector2.zero,new Vector2(2180,1070)); return rect.gameObject; }
        static Canvas Canvas(Transform parent,string name,Vector2 size,float scale=.001f)
        {
            var rect=Rect(parent,name,Vector2.zero,size); rect.localScale=Vector3.one*scale;
            var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=20;
            var ray=rect.gameObject.AddComponent<GraphicRaycaster>(); ray.blockingObjects=GraphicRaycaster.BlockingObjects.ThreeD; ray.blockingMask=UIBlockingMask();
            return canvas;
        }

        [MenuItem("ShuQi/Learning Hall/Apply Configuration")]
        public static void Apply()
        {
            Require(!EditorApplication.isPlaying,"Exit Play Mode first");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath);
            if(scene.isDirty) Require(EditorSceneManager.SaveScene(scene),"Cannot save current edits");
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/art/VRProjects/Fonts/NotoSansSC-Regular.otf"); Require(font,"Chinese font missing");
            ConfigureRig();
            var root=Find(scene,"LearningHallRoot"); Require(root,"LearningHallRoot missing");
            var feature=Find(scene,"LearningHallFeatures"); if(feature) Object.DestroyImmediate(feature.gameObject);
            feature=Group(root,"LearningHallFeatures").transform;
            var hall=feature.gameObject.AddComponent<LearningHallController>();
            hall.bench=Find(scene,"C_TrainingBench_ImportedModels"); hall.spawn=Find(scene,"Spawn_Learning");
            hall.trainingStand=Group(feature,"Stand_Training").transform; hall.trainingStand.position=new Vector3(0,0,-.15f);
            ConfigureUI(scene,hall);
            ConfigureTools(scene,hall);
            ConfigureCaseBoard(scene);
            var diagnostics=Find(scene,"LegacyDiagnostics_Preserved"); if(diagnostics) diagnostics.gameObject.SetActive(false);
            FixMaterials();
            EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene),"Cannot save learning hall");
            AssetDatabase.SaveAssets();
            Validate(); ExportViews();
        }

        static void ConfigureRig()
        {
            var rig=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var previous=rig.transform.Find("HallUI"); if(previous) Object.DestroyImmediate(previous.gameObject);
                var root=Group(rig.transform,"HallUI").transform;
                var driver=root.gameObject.AddComponent<HallRigUI>();
                driver.player=rig.GetComponentInChildren<AutoHandPlayer>(true); driver.teleporter=rig.GetComponentInChildren<Teleporter>(true);
                driver.leftController=rig.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Left Controller");
                driver.rightController=rig.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Right Controller");
                var system=Group(root,"Hall EventSystem"); system.AddComponent<EventSystem>(); system.AddComponent<AutoInputModule>();
                var pointer=Group(root,"UI Pointer"); pointer.layer=5;
                var line=pointer.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.positionCount=10; line.startWidth=.0025f; line.endWidth=.0012f;
                line.sharedMaterial=Material("HallUIRay",new Color(.15f,.95f,.86f),true); line.startColor=line.endColor=new Color(.15f,.95f,.86f); line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name="UI Cursor"; marker.transform.SetParent(pointer.transform,false); marker.transform.localScale=Vector3.one*.012f;
                Object.DestroyImmediate(marker.GetComponent<Collider>()); marker.GetComponent<Renderer>().sharedMaterial=line.sharedMaterial;
                driver.pointer=pointer.AddComponent<FingertipUIPointer>(); driver.pointer.raycastLength=4; driver.pointer.UILayer=1<<5; driver.pointer.hitPointMarker=marker; driver.pointer.useSmoothing=false;
                driver.pointer.finger=driver.player.handRight.fingers.Single(f=>f.fingerType==FingerEnum.index);
                var canvas=Canvas(root,"PauseCanvas",new Vector2(1000,700)); driver.pauseCanvas=canvas;
                var bg=Rect(canvas.transform,"Background",Vector2.zero,new Vector2(1000,700)).gameObject.AddComponent<Image>(); bg.color=Ink; bg.raycastTarget=false;
                Label(canvas.transform,"Title","术启 / 暂停与返回",new Vector2(0,285),new Vector2(920,80),46);
                driver.pauseStatus=Label(canvas.transform,"Status","右手 B 键暂停 / 继续",new Vector2(0,125),new Vector2(920,210),34);
                driver.resumeButton=Button(canvas.transform,"Resume","继续",new Vector2(0,-65),new Vector2(860,85));
                driver.returnButton=Button(canvas.transform,"ReturnHall","返回大厅 / 主页",new Vector2(0,-170),new Vector2(860,85));
                driver.quitButton=Button(canvas.transform,"SaveQuit","保存并退出",new Vector2(0,-275),new Vector2(860,85));
                canvas.gameObject.SetActive(false);
                var camera=rig.GetComponentInChildren<Camera>(true);
                var fade=Canvas(camera.transform,"SceneFade",new Vector2(2200,2200)); Object.DestroyImmediate(fade.GetComponent<GraphicRaycaster>());
                fade.transform.localPosition=new Vector3(0,0,.12f); fade.sortingOrder=1000;
                var black=Rect(fade.transform,"Black",Vector2.zero,new Vector2(2200,2200)).gameObject.AddComponent<Image>(); black.color=Color.black; black.raycastTarget=false;
                driver.fade=fade.gameObject.AddComponent<CanvasGroup>(); driver.fade.alpha=0; driver.fade.blocksRaycasts=false; driver.fade.interactable=false;
                PrefabUtility.SaveAsPrefabAsset(rig,RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
        }

        [MenuItem("ShuQi/Learning Hall/Use Fingertip UI Ray")]
        public static void ConfigureFingertipPointer()
        {
            var rig=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var driver=rig.GetComponentInChildren<HallRigUI>(true);
                var old=rig.GetComponentInChildren<HandCanvasPointer>(true);
                Require(driver && old,"Shared rig UI pointer missing");
                if(!(old is FingertipUIPointer))
                {
                    driver.pointer=old.gameObject.AddComponent<FingertipUIPointer>();
                    driver.pointer.hitPointMarker=old.hitPointMarker;
                    driver.pointer.raycastLength=old.raycastLength; driver.pointer.UILayer=old.UILayer;
                    driver.pointer.autoShowTarget=old.autoShowTarget;
                    driver.pointer.forwardPointerSmoothing=old.forwardPointerSmoothing;
                    driver.pointer.StartSelect=old.StartSelect; driver.pointer.StopSelect=old.StopSelect;
                    driver.pointer.StartPoint=old.StartPoint; driver.pointer.StopPoint=old.StopPoint;
                    Object.DestroyImmediate(old);
                }
                else driver.pointer=(FingertipUIPointer)old;
                driver.pointer.finger=driver.player.handRight.fingers.Single(f=>f.fingerType==FingerEnum.index);
                driver.pointer.useSmoothing=false;
                PrefabUtility.SaveAsPrefabAsset(rig,RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
        }

        static void ConfigureUI(Scene scene,LearningHallController hall)
        {
            var canvas=Find(scene,"EntryAndReviewCanvas").GetComponent<Canvas>();
            var legacy=canvas.transform.Find("LegacyStaticPanel");
            if(!legacy)
            {
                legacy=Group(canvas.transform,"LegacyStaticPanel").transform;
                foreach(Transform child in canvas.transform.Cast<Transform>().ToArray()) if(child!=legacy) child.SetParent(legacy,true);
                legacy.gameObject.SetActive(false);
            }
            var old=canvas.transform.Find("InteractiveHallUI"); if(old) Object.DestroyImmediate(old.gameObject);
            hall.uiRoot=Rect(canvas.transform,"InteractiveHallUI",Vector2.zero,new Vector2(2180,1070));
            if(!canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>().blockingObjects=GraphicRaycaster.BlockingObjects.ThreeD;
            canvas.GetComponent<GraphicRaycaster>().blockingMask=UIBlockingMask();
            var bg=Rect(hall.uiRoot,"Background",Vector2.zero,new Vector2(2180,1070)).gameObject.AddComponent<Image>(); bg.color=Ink; bg.raycastTarget=false;
            hall.homePage=Page(hall.uiRoot,"HomePage"); var home=hall.homePage.transform;
            Label(home,"Brand","术启  /  SURGICAL LEARNING HUB",new Vector2(-290,440),new Vector2(1400,80),43,TextAnchor.MiddleLeft);
            Button(home,"Save","保存",new Vector2(660,440),new Vector2(230,80)); Button(home,"Quit","退出",new Vector2(920,440),new Vector2(230,80));
            Label(home,"Title","从这里，开启第一台手术",new Vector2(0,315),new Vector2(2040,130),74);
            Button(home,"ModeGuided","引导学习\n跟随逐步提示",new Vector2(-515,145),new Vector2(970,155));
            Button(home,"ModeIndependent","自主练习\n保留求助与安全提示",new Vector2(515,145),new Vector2(970,155));
            Button(home,"OpenCase","认识病例",new Vector2(-765,-55),new Vector2(475,125));
            Button(home,"OpenCalibration","站位与台高",new Vector2(-255,-55),new Vector2(475,125));
            Button(home,"OpenPractice","器械练习",new Vector2(255,-55),new Vector2(475,125));
            Button(home,"OpenReview","操作复盘",new Vector2(765,-55),new Vector2(475,125));
            Button(home,"StartSurgery","开始手术  →  小手术操作室",new Vector2(0,-290),new Vector2(2000,170)).image.color=Teal;
            Label(home,"FreeEntry","自由选择，不设练习门槛 · CASE01 成人上背部浅表脂肪瘤",new Vector2(0,-420),new Vector2(2030,70),32);

            hall.detailPage=Page(hall.uiRoot,"DetailPage"); var detail=hall.detailPage.transform;
            hall.detailTitle=Label(detail,"DetailTitle","病例 / 操作复盘",new Vector2(0,440),new Vector2(2020,90),54);
            var viewport=Rect(detail,"Viewport",new Vector2(0,40),new Vector2(1970,650));
            var vi=viewport.gameObject.AddComponent<Image>(); vi.color=new Color(.055f,.13f,.17f); vi.raycastTarget=false;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"Content",Vector2.zero,new Vector2(1870,650)); content.anchorMin=new Vector2(.5f,1); content.anchorMax=new Vector2(.5f,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            hall.detailText=content.gameObject.AddComponent<Text>(); hall.detailText.font=font; hall.detailText.fontSize=37; hall.detailText.color=Light; hall.detailText.alignment=TextAnchor.UpperLeft; hall.detailText.raycastTarget=false; hall.detailText.verticalOverflow=VerticalWrapMode.Overflow;
            hall.detailScroll=viewport.gameObject.AddComponent<ScrollRect>(); hall.detailScroll.viewport=viewport; hall.detailScroll.content=content; hall.detailScroll.horizontal=false; hall.detailScroll.movementType=ScrollRect.MovementType.Clamped;
            hall.reviewActions=Group(detail,"ReviewActions");
            Button(hall.reviewActions.transform,"PreviousReport","上一条",new Vector2(-760,-345),new Vector2(400,90)); Button(hall.reviewActions.transform,"NextReport","下一条",new Vector2(-330,-345),new Vector2(400,90));
            Button(hall.reviewActions.transform,"ReviewPractice","大厅重练",new Vector2(200,-345),new Vector2(520,90)); Button(hall.reviewActions.transform,"RetryFullCase","重开完整病例",new Vector2(760,-345),new Vector2(520,90));
            Button(detail,"ScrollUp","上翻",new Vector2(-800,-445),new Vector2(350,80)); Button(detail,"ScrollDown","下翻",new Vector2(800,-445),new Vector2(350,80)); Button(detail,"DetailBack","返回主页",new Vector2(0,-445),new Vector2(780,80));

            hall.calibrationPage=Page(hall.uiRoot,"CalibrationPage"); var settings=hall.calibrationPage.transform;
            hall.settingsStatus=Label(settings,"SettingsStatus","S02 站位与舒适度",new Vector2(-450,70),new Vector2(1070,760),41,TextAnchor.UpperLeft);
            Button(settings,"BenchUp","台面 +2.5 cm",new Vector2(600,310),new Vector2(680,110)); Button(settings,"BenchDown","台面 −2.5 cm",new Vector2(600,170),new Vector2(680,110));
            Button(settings,"LeftHanded","左手 UI 指针",new Vector2(600,10),new Vector2(680,110)); Button(settings,"RightHanded","右手 UI 指针",new Vector2(600,-130),new Vector2(680,110));
            Button(settings,"Recenter","重定位到入口",new Vector2(-510,-345),new Vector2(920,110)); Button(settings,"TrainingStand","到训练台前",new Vector2(510,-345),new Vector2(920,110));
            Button(settings,"CalibrationBack","返回主页",new Vector2(0,-465),new Vector2(1000,75));

            hall.practicePage=Page(hall.uiRoot,"PracticePage"); var practice=hall.practicePage.transform;
            hall.practiceStatus=Label(practice,"PracticeStatus","S03 基础练习",new Vector2(0,100),new Vector2(1920,740),44,TextAnchor.UpperLeft);
            Button(practice,"StartPractice","开始 / 重试",new Vector2(-700,-330),new Vector2(600,125)); Button(practice,"ResetTools","复位器械",new Vector2(0,-330),new Vector2(600,125)); Button(practice,"PracticeToSurgery","开始手术",new Vector2(700,-330),new Vector2(600,125));
            Button(practice,"PracticeBack","返回主页（可随时离开）",new Vector2(0,-465),new Vector2(1700,75));
            hall.notice=Label(hall.uiRoot,"Notice","自由选择，无进入门槛",new Vector2(0,-520),new Vector2(2120,40),24);
            hall.detailPage.SetActive(false); hall.calibrationPage.SetActive(false); hall.practicePage.SetActive(false);
            foreach(var image in canvas.GetComponentsInChildren<Graphic>(true)) image.gameObject.layer=5;
            var status=Find(scene,"Status"); if(status) status.GetComponent<Text>().text="自由选择：认识病例 / 调整站位 / 基础练习 / 开始手术";
        }

        static Bounds Bounds(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>(true); Require(renderers.Length>0,"Model without renderers: "+obj.name);
            var b=renderers[0].bounds; foreach(var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            Directory.CreateDirectory("Assets/_Project/Materials/HallFeatures");
            string p="Assets/_Project/Materials/HallFeatures/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(!m) { m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,p); }
            m.color=color; return m;
        }
        static void SetPlacementRing(Renderer visual)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/art/LearningHall/PlacementRing.obj");
            Require(mesh && Mathf.Abs(mesh.bounds.size.x-1)<.001f && Mathf.Abs(mesh.bounds.size.z-1)<.001f,"Placement ring mesh missing or incorrectly scaled");
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.transform.localScale=new Vector3(.24f,.002f,.24f);
        }
        [MenuItem("ShuQi/Learning Hall/Use Hollow Placement Rings")]
        public static void ConfigurePlacementRings()
        {
            Require(!EditorApplication.isPlaying,"Exit Play Mode first");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var hall=Find(scene,"LearningHallFeatures").GetComponent<LearningHallController>();
            Require(hall && hall.tools.Length==3,"Expected three demonstration tools");
            var reports=new List<string>();
            foreach(var tool in hall.tools)
            {
                Require(tool && tool.home && tool.slotRenderer && tool.slotRenderer.GetComponent<MeshFilter>(),"Placement visual missing");
                Require(!tool.slotRenderer.GetComponent<Collider>(),"Placement visual must not block the tool");
                SetPlacementRing(tool.slotRenderer);
                reports.Add("PASS "+tool.toolName+" hollow ring: outer 24 cm, inner 22.56 cm; existing renderer and placement references preserved");
            }
            EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene),"Cannot save placement rings");
            File.WriteAllLines(Work+"/placement-rings.txt",reports); ExportViews();
        }
        static HallTool MakeTool(Scene scene,LearningHallController hall,string modelName,string rootName,string display,Transform homes)
        {
            var wrapper=Find(scene,rootName);
            if(!wrapper)
            {
                var model=Find(scene,modelName); Require(model,"Missing tool "+modelName);
                var b=Bounds(model.gameObject);
                wrapper=Group(model.parent,rootName).transform; wrapper.position=b.center;
                model.SetParent(wrapper,true);
                foreach(var collider in wrapper.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var box=wrapper.gameObject.AddComponent<BoxCollider>(); box.center=Vector3.zero; box.size=new Vector3(Mathf.Max(.008f,b.size.x),Mathf.Max(.008f,b.size.y),Mathf.Max(.008f,b.size.z));
                var body=wrapper.gameObject.AddComponent<Rigidbody>(); body.mass=.08f; body.drag=.15f; body.angularDrag=.8f; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic; body.interpolation=RigidbodyInterpolation.Interpolate;
                var grab=wrapper.gameObject.AddComponent<Grabbable>(); grab.singleHandOnly=true; grab.allowHeldSwapping=true; grab.throwPower=.35f; grab.makeChildrenGrabbable=true;
                wrapper.gameObject.AddComponent<HallTool>();
            }
            foreach(var child in wrapper.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=LayerMask.NameToLayer("Grabbable");
            var tool=wrapper.GetComponent<HallTool>(); tool.hall=hall; tool.toolName=display;
            tool.home=Group(homes,"Place_"+rootName.Replace("Training_","")).transform; tool.home.SetPositionAndRotation(wrapper.position,wrapper.rotation);
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name="归位轮廓_"+display; visual.transform.SetParent(tool.home,false); visual.transform.localPosition=new Vector3(0,-.018f,0); Object.DestroyImmediate(visual.GetComponent<Collider>());
            tool.slotRenderer=visual.GetComponent<Renderer>(); tool.slotRenderer.sharedMaterial=Material("PlacementTarget",new Color(.2f,.48f,.5f)); tool.slotRenderer.shadowCastingMode=ShadowCastingMode.Off;
            SetPlacementRing(tool.slotRenderer);
            return tool;
        }
        static void ConfigureTools(Scene scene,LearningHallController hall)
        {
            var previous=hall.bench.Find("HallPlacementTargets"); if(previous) Object.DestroyImmediate(previous.gameObject);
            var homes=Group(hall.bench,"HallPlacementTargets").transform;
            hall.tools=new[]{MakeTool(scene,hall,"Scalpel_Assembly_Layout","Training_Scalpel","手术刀（示教）",homes),MakeTool(scene,hall,"SM_OPR_Thumb_Tissue_Forceps_01","Training_Forceps","镊子（示教）",homes),MakeTool(scene,hall,"SM_OPR_Surgical_Scissors_01","Training_Scissors","剪刀（示教）",homes)};
            hall.PracticeTool=hall.tools[1];
            var mat=Find(scene,"TrainingMat");
            if(mat && !mat.GetComponent<BoxCollider>()) mat.gameObject.AddComponent<BoxCollider>();
            foreach(var table in hall.bench.Cast<Transform>().Where(t=>t.name.StartsWith("SM_OPR_Table_02")))
            {
                if(table.Find("HallSurfaceCollider")) continue;
                var b=Bounds(table.gameObject); var surface=Group(table,"HallSurfaceCollider"); surface.transform.position=new Vector3(b.center.x,b.max.y-.02f,b.center.z);
                var box=surface.AddComponent<BoxCollider>(); box.size=new Vector3(b.size.x,.04f,b.size.z);
            }
            var floor=Find(scene,"LearningHallRoot").Find("01_RoomShell_PrimitivePlaceholder/Floor"); floor.gameObject.layer=LayerMask.NameToLayer("TeleportFloor");
            var target=Group(homes,"RotationTarget").transform; target.position=new Vector3(.18f,1.3f,.32f); target.rotation=Quaternion.Euler(0,0,90);
            hall.rotationTarget=target;
            var ghost=Object.Instantiate(hall.PracticeTool.transform.GetChild(0).gameObject,target,false); ghost.name="方向示意_待教学美术替换";
            foreach(var renderer in ghost.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>Material("RotationTarget",new Color(.96f,.7f,.2f),true)).ToArray();
            target.gameObject.SetActive(false);
        }
        static void ConfigureCaseBoard(Scene scene)
        {
            var summary=Find(scene,"CaseSummary");
            if(summary) summary.GetComponent<Text>().text="成人 · 单发 · 局麻教学病例\n\n术前准备 → 切除\n关闭创口 → 术后照护\n\n医学文案待审核\n详情见主面板“认识病例”";
        }
        static void FixMaterials()
        {
            var notes=new List<string>();
            const string textures="Assets/_Project/art/VRProjects/OperatingRoom/Textures/";
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/art/VRProjects/OperatingRoom/Material_Instances"}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); if(!m || !m.HasProperty("_BumpMap")) continue;
                string old=AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap"));
                if(!old.EndsWith("_BaseColor.png",StringComparison.OrdinalIgnoreCase)) continue;
                string prefix=Path.GetFileNameWithoutExtension(old).Replace("_BaseColor","");
                var color=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_BaseColor.png");
                var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_Normal.png");
                if(!color || !normal) { notes.Add("待补贴图："+m.name); continue; }
                var normalImport=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                if(normalImport.textureType!=TextureImporterType.NormalMap) { normalImport.textureType=TextureImporterType.NormalMap; normalImport.SaveAndReimport(); }
                m.SetTexture("_BaseMap",color); m.SetTexture("_MainTex",color); m.SetTexture("_BumpMap",normal); m.EnableKeyword("_NORMALMAP");
                // Source ORM uses R=occlusion, G=roughness, B=metallic; URP reads a different channel layout.
                var orm=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_OcclusionRoughnessMetallic.png");
                if(orm)
                {
                    string sourcePath=AssetDatabase.GetAssetPath(orm); var importer=(TextureImporter)AssetImporter.GetAtPath(sourcePath);
                    bool readable=importer.isReadable; int max=importer.maxTextureSize; bool srgb=importer.sRGBTexture;
                    try
                    {
                        importer.isReadable=true; importer.maxTextureSize=1024; importer.sRGBTexture=false; importer.SaveAndReimport(); orm=AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                        var pixels=orm.GetPixels32();
                        for(int i=0;i<pixels.Length;i++) { var p=pixels[i]; pixels[i]=new Color32(p.b,p.r,0,(byte)(255-p.g)); }
                        var mask=new Texture2D(orm.width,orm.height,TextureFormat.RGBA32,false,true); mask.SetPixels32(pixels); mask.Apply();
                        string path="Assets/_Project/Materials/HallFeatures/"+prefix+"_URP_Mask.png"; File.WriteAllBytes(path,mask.EncodeToPNG()); Object.DestroyImmediate(mask); AssetDatabase.ImportAsset(path);
                        var mi=(TextureImporter)AssetImporter.GetAtPath(path); mi.sRGBTexture=false; mi.maxTextureSize=1024; mi.SaveAndReimport();
                        var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(path); m.SetTexture("_MetallicGlossMap",packed); m.SetTexture("_OcclusionMap",packed); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_Smoothness",1); m.SetFloat("_OcclusionStrength",.5f);
                    }
                    finally { importer.isReadable=readable; importer.maxTextureSize=max; importer.sRGBTexture=srgb; importer.SaveAndReimport(); }
                }
                EditorUtility.SetDirty(m); notes.Add("已修正："+m.name+" → "+prefix);
            }
            File.WriteAllLines(Work+"/material-fixes.txt",notes);
        }

        [MenuItem("ShuQi/Learning Hall/Validate Configuration")]
        public static void Validate()
        {
            var reports=new List<string>();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(string name in new[]{"学习大厅","小手术操作室","复诊室"})
                {
                    var scene=SceneManager.GetSceneByName(name); bool opened=!scene.isLoaded;
                    if(opened) scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity",OpenSceneMode.Additive);
                    var rig=Find(scene,"QuestRig"); Require(rig,"Rig missing: "+name);
                    Require(rig.GetComponentsInChildren<HallRigUI>(true).Length==1,"UI rig missing/duplicated: "+name);
                    Require(rig.GetComponentsInChildren<EventSystem>(true).Length==1,"EventSystem missing/duplicated: "+name);
                    Require(rig.GetComponentsInChildren<HandDistanceGrabber>(true).Length==0,"Distance grabbing was introduced");
                    Require(rig.GetComponentsInChildren<Hand>(true).Length==2,"Hand models missing");
                    var pointer=rig.GetComponentInChildren<FingertipUIPointer>(true);
                    Require(pointer && pointer.finger && !pointer.useSmoothing,"Fingertip UI ray missing: "+name);
                    foreach(var hand in rig.GetComponentsInChildren<Hand>(true))
                    {
                        var index=hand.fingers.Single(f=>f.fingerType==FingerEnum.index);
                        Require(!index.isMissingReferences && Vector3.Distance(index.tip.position,index.distalJoint.position)>.001f,"Index fingertip setup invalid: "+hand.name);
                        Require(index.IsPoseSaved(FingerPoseEnum.Open) && index.poseData[(int)FingerPoseEnum.Open].poseRelativeMatrix.Length==4,"Open index pose missing for fixed UI ray: "+hand.name);
                    }
                    if(name=="学习大厅")
                    {
                        var hall=Find(scene,"LearningHallFeatures").GetComponent<LearningHallController>(); Require(hall && hall.tools.Length==3,"Hall configuration missing");
                        foreach(var tool in hall.tools)
                        {
                            Require(tool && tool.home && tool.GetComponent<Rigidbody>() && tool.GetComponent<Grabbable>(),"Tool interaction incomplete");
                            Require(!tool.GetComponentsInChildren<MeshCollider>(true).Any(c=>!c.convex),"Dynamic concave mesh on "+tool.name);
                            reports.Add(tool.toolName+" | position="+tool.transform.position+" | collider="+tool.GetComponent<BoxCollider>().size);
                        }
                        var buttons=hall.uiRoot.GetComponentsInChildren<Button>(true);
                        foreach(string required in new[]{"ModeGuided","ModeIndependent","StartSurgery","OpenCase","OpenCalibration","OpenPractice","OpenReview","StartPractice","ResetTools","DetailBack","RetryFullCase"})
                            Require(buttons.Count(b=>b.name==required)==1,"Hall button missing or duplicated: "+required);
                    }
                    foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script "+t.name);
                    reports.Add("PASS "+name+" shared UI, hand models, no distance grab, no missing scripts");
                    if(opened) EditorSceneManager.CloseScene(scene,true);
                }
            }
            finally { }
            File.WriteAllLines(Work+"/scene-validation.txt",reports);
        }
        static void ExportViews()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath); if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath);
            var camera=new GameObject("Temporary Hall Preview").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
            camera.transform.position=new Vector3(0,1.65f,-.65f); camera.transform.rotation=Quaternion.identity; camera.fieldOfView=78; camera.nearClipPlane=.04f; camera.farClipPlane=20;
            var target=new RenderTexture(1440,1000,24); var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); var old=RenderTexture.active; RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1440,1000),0,0); texture.Apply(); RenderTexture.active=old;
                File.WriteAllBytes(Work+"/hall-configured-view.png",texture.EncodeToPNG());
                camera.transform.position=new Vector3(.1f,1.85f,-.3f); camera.transform.LookAt(new Vector3(.1f,.93f,.65f)); camera.fieldOfView=56;
                camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1440,1000),0,0); texture.Apply(); RenderTexture.active=old;
                File.WriteAllBytes(Work+"/placement-rings-view.png",texture.EncodeToPNG());
            }
            finally { camera.targetTexture=null; Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(target); }
        }
        static int UIBlockingMask() => ~LayerMask.GetMask("Hand","HandPlayer","Grabbing","Ignore Raycast");
        static void FinishMaterials()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/art/VRProjects/OperatingRoom/Material_Instances"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!material || !material.HasProperty("_MetallicGlossMap")) continue;
                if(!AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")).Contains("HallFeatures/")) continue;
                material.SetTexture("_MetallicGlossMap",null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic",material.name.Contains("Surgical_Set")?.45f:.12f);
                material.SetFloat("_Smoothness",material.name.Contains("Glass")?.65f:.35f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets(); ExportViews();
        }
        static void Polish()
        {
            var active=SceneManager.GetActiveScene();
            if(active.isDirty) Require(EditorSceneManager.SaveScene(active),"Save current edits before polish");
            var rig=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                foreach(var ray in rig.GetComponentsInChildren<GraphicRaycaster>(true)) ray.blockingMask=UIBlockingMask();
                PrefabUtility.SaveAsPrefabAsset(rig,RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
            string original=active.path;
            var reports=new List<string>();
            try
            {
                foreach(string name in new[]{"学习大厅","小手术操作室","复诊室"})
                {
                    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity",OpenSceneMode.Single);
                    foreach(var root in scene.GetRootGameObjects()) foreach(var ray in root.GetComponentsInChildren<GraphicRaycaster>(true)) ray.blockingMask=UIBlockingMask();
                    var existing=Find(scene,"RoomBakedReflection");
                    var probe=existing?existing.GetComponent<ReflectionProbe>():new GameObject("RoomBakedReflection").AddComponent<ReflectionProbe>();
                    probe.transform.position=new Vector3(0,1.45f,.2f); probe.size=new Vector3(18,5,18); probe.center=Vector3.zero;
                    probe.mode=ReflectionProbeMode.Baked; probe.resolution=128; probe.intensity=1; probe.boxProjection=true;
                    probe.clearFlags=ReflectionProbeClearFlags.SolidColor; probe.backgroundColor=new Color(.7f,.75f,.78f); probe.cullingMask=UIBlockingMask() & ~(1<<5); probe.nearClipPlane=.1f; probe.farClipPlane=25;
                    string path="Assets/_Project/Settings/"+name+"_Reflection.exr";
                    Require(Lightmapping.BakeReflectionProbe(probe,path),"Reflection bake failed: "+name);
                    AssetDatabase.ImportAsset(path); probe.bakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);
                    Require(probe.bakedTexture,"Reflection texture missing: "+name);
                    EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene),"Save failed: "+name);
                    reports.Add(name+" UI ignores hand/player colliders; baked reflection 128px");
                }
            }
            finally { EditorSceneManager.OpenScene(string.IsNullOrEmpty(original)?ScenePath:original); }
            AssetDatabase.SaveAssets(); File.WriteAllLines(Work+"/polish-result.txt",reports); ExportViews();
        }
    }
}
