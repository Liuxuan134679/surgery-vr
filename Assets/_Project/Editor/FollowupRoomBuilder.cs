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
    public static class FollowupRoomBuilder
    {
        const string ScenePath="Assets/_Project/Scenes/复诊室.unity";
        const string HallPath="Assets/_Project/Scenes/学习大厅.unity";
        const string Imported="Assets/_Project/art/VRProjects";
        const string Art="Assets/_Project/art/FollowupRoom";
        const string Request="Temp/ShuqiFollowupRoom.request";
        const string Reports="D:/CGJ/outputs/shuqi_followup_room";
        static readonly string[] Models={"SM__SmallTable","SM__OfficeChair","SM_OPR_Chair_02","SM__TVScreen"};
        static Font font;
        static Material ink,teal,pale,white,metal,skin,cloth;

        [Serializable] class Manifest { public string[] new_paths; }
        static FollowupRoomBuilder(){EditorApplication.delayCall+=RunPending;}
        static void RunPending()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=RunPending;return;}
            string action=File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                PrepareNewAssets();
                InspectModels();
                if(action=="build")Build();
                File.WriteAllText(Reports+"/editor-result.txt","PASS: "+action+"\n"+DateTime.Now.ToString("s"));
                Debug.Log("SHUQI_FOLLOWUP_ROOM_PASS "+action);
            }
            catch(Exception error){File.WriteAllText(Reports+"/editor-result.txt","FAIL\n"+error);Debug.LogException(error);}
        }

        static void PrepareNewAssets()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Reports+"/asset_manifest.json"));
            foreach(string path in manifest.new_paths)
            {
                var texture=AssetImporter.GetAtPath(path) as TextureImporter;
                if(texture!=null)
                {
                    bool normal=path.EndsWith("_N.png",StringComparison.OrdinalIgnoreCase)||path.Contains("_Normal");
                    bool packed=path.Contains("_RMO")||path.Contains("OcclusionRoughnessMetallic");
                    if(texture.maxTextureSize!=1024||texture.isReadable||(normal&&texture.textureType!=TextureImporterType.NormalMap)||(!normal&&!packed&&texture.textureType==TextureImporterType.NormalMap)||(packed&&texture.sRGBTexture))
                    {
                        texture.maxTextureSize=1024;texture.isReadable=false;texture.mipmapEnabled=true;texture.textureCompression=TextureImporterCompression.Compressed;
                        if(normal)texture.textureType=TextureImporterType.NormalMap;
                        else if(!packed){texture.textureType=TextureImporterType.Default;texture.sRGBTexture=true;}
                        if(packed)texture.sRGBTexture=false;
                        texture.SaveAndReimport();
                    }
                }
                if(!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                Require(material,"Missing new material: "+path);
                material.shader=Shader.Find("Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.25f);material.SetFloat("_Metallic",.06f);
                if(material.GetTexture("_BumpMap"))material.EnableKeyword("_NORMALMAP");
                // RMO channels differ from URP occlusion; retain the base and normal maps.
                material.SetTexture("_OcclusionMap",null);material.DisableKeyword("_OCCLUSIONMAP");
                material.enableInstancing=true;EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        static Bounds BoundsOf(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length>0,"No model renderers: "+obj.name);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static void InspectModels()
        {
            var scene=EditorSceneManager.NewPreviewScene();var lines=new List<string>();
            try
            {
                foreach(string name in Models)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"/Prefabs/"+name+".prefab");Require(prefab,"Missing imported model: "+name);
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var b=BoundsOf(obj);
                    long triangles=obj.GetComponentsInChildren<MeshFilter>(true).Sum(m=>m.sharedMesh?Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i=>(long)m.sharedMesh.GetIndexCount(i)/3):0);
                    lines.Add(name+" | size "+b.size.ToString("F4")+" | triangles (all LODs) "+triangles);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            File.WriteAllLines(Reports+"/model-inspection.txt",lines);
        }

        static void Build()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"请先保存当前场景的未保存修改，再搭建复诊室。");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var old=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="FollowupRoomRoot");if(old)Object.DestroyImmediate(old);
            var oldLight=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="FollowupFillLight");if(oldLight)Object.DestroyImmediate(oldLight);
            var legacy=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="LegacySceneSetup_Preserved");
            if(!legacy){legacy=new GameObject("LegacySceneSetup_Preserved");foreach(var c in scene.GetRootGameObjects().Where(o=>o.GetComponent<Camera>()).ToArray())c.transform.SetParent(legacy.transform,true);legacy.SetActive(false);}
            Directory.CreateDirectory(Art+"/Materials");AssetDatabase.Refresh();
            font=AssetDatabase.LoadAssetAtPath<Font>(Imported+"/Fonts/NotoSansSC-Regular.otf");Require(font,"Missing shared Chinese font");
            Require(font.material.shader.name!="Universal Render Pipeline/Lit","The shared font must retain its text shader");
            ink=Mat("Ink",new Color(.035f,.095f,.13f));teal=Mat("Teal",new Color(.04f,.46f,.48f));
            pale=Mat("Pale",new Color(.71f,.87f,.85f));white=Mat("White",new Color(.94f,.97f,.96f));
            metal=Mat("Metal",new Color(.48f,.56f,.58f));skin=Mat("PatientNeutral",new Color(.72f,.68f,.59f));cloth=Mat("PatientClothing",new Color(.19f,.39f,.42f));
            var root=new GameObject("FollowupRoomRoot");
            var shell=CloneHallShell(root.transform,scene);

            var desk=Group(root.transform,"A_ConsultationDesk_ReportArea");
            Box(desk,"DeskTop_PrimitivePlaceholder",new Vector3(-.48f,.78f,.55f),new Vector3(1.6f,.055f,.74f),white);
            Box(desk,"DeskModestyPanel",new Vector3(-.48f,.48f,.83f),new Vector3(1.4f,.5f,.035f),pale);
            Box(desk,"DeskLeg_Left",new Vector3(-1.13f,.38f,.55f),new Vector3(.10f,.75f,.62f),metal);
            Box(desk,"DeskLeg_Right",new Vector3(.17f,.38f,.55f),new Vector3(.10f,.75f,.62f),metal);
            Place("SM__OfficeChair",desk,new Vector3(-.48f,0,1.35f),180,1.06f);
            var screen=Place("SM__TVScreen",desk,new Vector3(-.48f,.81f,.56f),0,.47f);
            var sb=BoundsOf(screen);
            if(sb.size.z>sb.size.x){screen.transform.Rotate(0,90,0,Space.World);sb=BoundsOf(screen);}
            Box(desk,"MonitorStand_PrimitivePlaceholder",new Vector3(-.48f,.825f,.56f),new Vector3(.30f,.025f,.18f),metal);
            var report=CanvasAt("SimulatedPathologyReport",desk,new Vector3(sb.center.x,sb.center.y,sb.min.z-.009f),new Vector2(sb.size.x*.88f,sb.size.y*.83f));
            Image(report,"Page",Vector2.zero,((RectTransform)report).sizeDelta,new Color(.92f,.96f,.95f));
            float rw=((RectTransform)report).sizeDelta.x,rh=((RectTransform)report).sizeDelta.y;
            Text(report,"ReportTitle","模拟病理报告",new Vector2(0,rh*.30f),new Vector2(rw*.9f,rh*.25f),42,new Color(.04f,.32f,.34f));
            Text(report,"ReportFields","病例  LIP-UB-01\n标本部位：上背部\n结果：由教学病例配置",new Vector2(0,-rh*.12f),new Vector2(rw*.86f,rh*.55f),27,new Color(.12f,.24f,.27f));

            var board=Group(root.transform,"FollowupBriefingPanel_LayoutPlaceholder");
            Box(board,"PanelFrame",new Vector3(-.48f,1.91f,2.34f),new Vector3(1.90f,1.10f,.055f),ink,false);
            var canvas=CanvasAt("FollowupCanvas",board,new Vector3(-.48f,1.91f,2.305f),new Vector2(1.82f,1.02f));
            Image(canvas,"Background",Vector2.zero,new Vector2(1820,1020),new Color(.035f,.095f,.13f));
            Text(canvas,"SceneCode","术启 / FOLLOW-UP ROOM",new Vector2(0,405),new Vector2(1620,100),49,Color.white,TextAnchor.MiddleLeft);
            Text(canvas,"Title","复诊与结果沟通",new Vector2(0,220),new Vector2(1620,140),88,Color.white,TextAnchor.MiddleLeft);
            Text(canvas,"Context","S59  ·  时间跳转至复诊日  ·  模拟病例",new Vector2(0,80),new Vector2(1620,85),42,new Color(.61f,.84f,.83f),TextAnchor.MiddleLeft);
            string[] names={"查看报告","复查伤口","返回复盘"};
            for(int i=0;i<3;i++){float x=(i-1)*540;Image(canvas,"ActionCard_"+i,new Vector2(x,-155),new Vector2(510,210),i==1?new Color(.04f,.46f,.48f):new Color(.10f,.21f,.25f));Text(canvas,"ActionLabel_"+i,names[i],new Vector2(x,-155),new Vector2(480,140),58,Color.white);}
            Text(canvas,"Footer","同一病例 · 伤口复查 · 病理反馈 · 后续安排",new Vector2(0,-405),new Vector2(1620,90),36,new Color(.65f,.81f,.81f));

            var exam=Group(root.transform,"B_BackExaminationArea");
            Place("SM_OPR_Chair_02",exam,new Vector3(1.35f,0,.05f),0,.47f);
            var patient=Group(exam,"Patient_PrimitivePlaceholder");patient.position=new Vector3(1.35f,0,.05f);
            var torso=Group(patient,"UpperBody_LeanForward");torso.localPosition=new Vector3(0,.82f,.05f);torso.localRotation=Quaternion.Euler(12,0,0);
            Shape(torso,"UpperBackSkin",PrimitiveType.Capsule,Vector3.zero,new Vector3(.36f,.26f,.25f),skin);
            Shape(torso,"Neck",PrimitiveType.Capsule,new Vector3(0,.30f,.015f),new Vector3(.09f,.055f,.09f),skin);
            Shape(torso,"Head",PrimitiveType.Sphere,new Vector3(0,.40f,.015f),Vector3.one*.205f,skin);
            Shape(torso,"Hair",PrimitiveType.Sphere,new Vector3(0,.435f,.02f),new Vector3(.21f,.15f,.20f),ink);
            Shape(patient,"LowerTorsoClothing",PrimitiveType.Capsule,new Vector3(0,.58f,.02f),new Vector3(.36f,.15f,.27f),cloth);
            for(int side=-1;side<=1;side+=2)
            {
                var thigh=Shape(patient,"Thigh_"+side,PrimitiveType.Capsule,new Vector3(.12f*side,.47f,.23f),new Vector3(.13f,.18f,.13f),cloth);thigh.transform.localRotation=Quaternion.Euler(90,0,0);
                Shape(patient,"Shin_"+side,PrimitiveType.Capsule,new Vector3(.12f*side,.24f,.39f),new Vector3(.105f,.18f,.105f),cloth);
                Shape(patient,"Foot_"+side,PrimitiveType.Cube,new Vector3(.12f*side,.055f,.46f),new Vector3(.12f,.10f,.24f),ink);
                var arm=Shape(torso,"Arm_"+side,PrimitiveType.Capsule,new Vector3(.245f*side,-.02f,.10f),new Vector3(.095f,.19f,.095f),skin);arm.transform.localRotation=Quaternion.Euler(20,0,side*12);
            }
            var patch=Shape(torso,"PostopPatch_PrimitivePlaceholder",PrimitiveType.Cube,new Vector3(0,.09f,-.129f),new Vector3(.16f,.11f,.014f),white);
            Shape(torso,"WoundReviewMarker",PrimitiveType.Cube,new Vector3(0,.09f,-.139f),new Vector3(.085f,.025f,.005f),teal);
            var target=patch.AddComponent<BoxCollider>();target.size=Vector3.one;
            var sideTable=Place("SM__SmallTable",exam,new Vector3(2.03f,0,.98f),0,.56f);
            var tb=BoundsOf(sideTable);
            Box(exam,"CareLeaflet_PrimitivePlaceholder",new Vector3(2.03f,tb.max.y+.006f,.98f),new Vector3(.21f,.009f,.15f),white,false);
            var care=CanvasAt("CarePlanLabel",exam,new Vector3(1.35f,1.62f,.38f),new Vector2(.95f,.23f));
            Text(care,"ExaminationLabel","上背部复查区",Vector2.zero,new Vector2(900,200),78,new Color(.04f,.29f,.31f));

            var storage=Group(root.transform,"C_StorageAndRoomDetails");
            Place("SM_OPR_Cabinet_01_Full",storage,new Vector3(-1.90f,0,1.82f),90,1.20f);
            var sign=CanvasAt("CaseInformation",storage,new Vector3(-2.22f,1.69f,1.22f),new Vector2(.75f,.26f));
            Text(sign,"CaseID","标准病例\nLIP-UB-01",Vector2.zero,new Vector2(740,240),59,new Color(.04f,.29f,.31f));

            var markers=Group(root.transform,"D_PlayerStandpoints");
            FloorMark(markers,"ReportPosition",new Vector3(0,0,-1.54f),new Vector2(.85f,.67f),"报告观察位");
            FloorMark(markers,"BackReviewPosition",new Vector3(1.35f,0,-1.13f),new Vector2(.75f,.73f),"复查观察位");
            var spawn=Group(markers,"Spawn_Followup");spawn.position=new Vector3(0,0,-1.54f);spawn.rotation=Quaternion.Euler(0,10,0);
            var back=Group(markers,"Spawn_BackReview");back.position=new Vector3(1.35f,0,-1.13f);
            var rig=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="QuestRig");
            if(!rig)rig=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/QuestRig.prefab"),scene);
            Require(rig,"Missing QuestRig prefab");rig.transform.SetPositionAndRotation(spawn.position,spawn.rotation);
            foreach(var r in rig.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            var xrCamera=rig.GetComponentInChildren<Camera>(true);xrCamera.nearClipPlane=.04f;xrCamera.farClipPlane=30;

            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.70f,.76f,.78f);RenderSettings.skybox=null;RenderSettings.fog=false;
            var sun=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun){sun.intensity=.75f;sun.color=new Color(1,.98f,.95f);sun.shadows=LightShadows.None;sun.transform.rotation=Quaternion.Euler(45,-30,0);}
            var fill=new GameObject("FollowupFillLight").AddComponent<Light>();fill.transform.position=new Vector3(0,2.6f,-.3f);fill.type=LightType.Point;fill.range=7;fill.intensity=.9f;fill.shadows=LightShadows.None;
            AssetDatabase.SaveAssets();Canvas.ForceUpdateCanvases();
            Validate(scene);
            Require(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save follow-up room");
            ExportViews(scene,shell);
            foreach(var t in shell.GetComponentsInChildren<Transform>())if(t.name.StartsWith("Ceiling")||t.name.StartsWith("SouthWall_")||t.name.StartsWith("Door"))SceneVisibilityManager.instance.Hide(t.gameObject,true);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(new Vector3(0,1.1f,.35f),Quaternion.Euler(27,12,0),3.0f);
        }

        static Transform CloneHallShell(Transform parent,Scene target)
        {
            var preview=EditorSceneManager.OpenScene(HallPath,OpenSceneMode.Additive);
            try
            {
                var source=preview.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="01_RoomShell_PrimitivePlaceholder");
                var clone=Object.Instantiate(source.gameObject);SceneManager.MoveGameObjectToScene(clone,target);clone.name=source.name;clone.transform.SetParent(parent,false);clone.transform.localScale=new Vector3(.84f,1,.75f);return clone.transform;
            }
            finally{EditorSceneManager.CloseScene(preview,true);}
        }
        static GameObject Place(string name,Transform parent,Vector3 bottom,float yaw,float height)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"/Prefabs/"+name+".prefab");Require(prefab,"Missing model "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);obj.transform.rotation=Quaternion.Euler(0,yaw,0);
            var b=BoundsOf(obj);Require(b.size.y>.001f,"Invalid model bounds: "+name);obj.transform.localScale*=height/b.size.y;
            b=BoundsOf(obj);obj.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);return obj;
        }
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Material Mat(string name,Color color)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collider=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static Transform CanvasAt(string name,Transform parent,Vector3 position,Vector2 meters)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Canvas));obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=Vector3.one*.001f;((RectTransform)obj.transform).sizeDelta=meters*1000;obj.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;return obj.transform;
        }
        static void Image(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchoredPosition=pos;rect.sizeDelta=size;var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
        }
        static void Text(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchoredPosition=pos;rect.sizeDelta=size;var label=obj.GetComponent<UnityEngine.UI.Text>();label.font=font;label.fontSize=fontSize;label.text=value;label.color=color;label.alignment=alignment;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
        }
        static void FloorMark(Transform parent,string name,Vector3 pos,Vector2 size,string label)
        {
            Box(parent,name,pos+Vector3.up*.008f,new Vector3(size.x,.009f,size.y),pale,false);var canvas=CanvasAt(name+"Label",parent,pos+new Vector3(0,.017f,-.12f),new Vector2(size.x,.16f));canvas.rotation=Quaternion.Euler(90,0,0);Text(canvas,name+"Text",label,Vector2.zero,new Vector2(size.x*1000,140),65,new Color(.04f,.29f,.31f));
        }
        static void Validate(Scene scene)
        {
            var objects=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            Require(objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount)==0,"Missing scripts in follow-up room");
            Require(objects.SelectMany(o=>o.GetComponents<Camera>()).Count(c=>c.isActiveAndEnabled)==1,"Expected one active XR camera");
            Require(objects.SelectMany(o=>o.GetComponents<AudioListener>()).Count(l=>l.isActiveAndEnabled)==1,"Expected one active audio listener");
            var renderers=objects.Where(o=>o.activeInHierarchy).SelectMany(o=>o.GetComponents<Renderer>()).ToArray();
            Require(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.name!="Hidden/InternalErrorShader")),"Invalid scene material or shader");
            foreach(string name in new[]{"Spawn_Followup","Spawn_BackReview","SimulatedPathologyReport","Patient_PrimitivePlaceholder","FollowupCanvas"})Require(objects.Any(o=>o.name==name),"Missing follow-up zone: "+name);
            File.WriteAllText(Reports+"/scene-validation.txt","PASS\nScene: "+ScenePath+"\nActive renderers: "+renderers.Length+"\nActive XR cameras: 1\nMissing scripts: 0\nScope: S59 scene and static UI layout; interaction and clinical content pending.\nQuest 3 hardware: NOT TESTED\n");
        }
        static void ExportViews(Scene scene,Transform shell)
        {
            var go=new GameObject("FollowupPreviewCamera_EditorOnly");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=35;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.86f,.85f);
            var roof=shell.Find("Ceiling_CutawayForPreview");
            try
            {
                Render(camera,new Vector3(0,1.65f,-1.54f),new Vector3(.20f,1.38f,.60f),false,"followup-player-view.png");
                Render(camera,new Vector3(-2.22f,2.05f,-1.95f),new Vector3(.3f,1.05f,.65f),false,"followup-overview.png");
                roof.gameObject.SetActive(false);Render(camera,new Vector3(0,9,.15f),new Vector3(0,0,.151f),true,"followup-top-view.png");
            }
            finally{roof.gameObject.SetActive(true);Object.DestroyImmediate(go);}
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static void Render(Camera camera,Vector3 position,Vector3 target,bool ortho,string name)
        {
            camera.transform.position=position;camera.transform.LookAt(target);camera.orthographic=ortho;camera.orthographicSize=2.85f;camera.fieldOfView=68;
            var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Reports+"/"+name,image.EncodeToPNG());Object.DestroyImmediate(image);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
