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
    public static class ProcedureRoomBuilder
    {
        const string ScenePath="Assets/_Project/Scenes/小手术操作室.unity";
        const string HallPath="Assets/_Project/Scenes/学习大厅.unity";
        const string Imported="Assets/_Project/art/VRProjects";
        const string Art="Assets/_Project/art/ProcedureRoom";
        const string Request="Temp/ShuqiProcedureRoom.request";
        const string Reports="D:/CGJ/outputs/shuqi_procedure_room";
        const string PatientPath=Imported+"/清华老女人/lao-QingHuaJiZhu.fbx";
        const string SinkPath=Imported+"/OperatingRoom/ss-wash.FBX";
        const string NeedlePath=Imported+"/!Prepare Test/!Workspace/Arts/Model/Instrument/SurgicalTools/Models/StitchesNeedle.fbx";
        static Font font;
        static Material ink,teal,pale,white,metal,skin,cloth,amber;
        [Serializable] class Manifest { public string[] new_paths; public string[] models; }
        static ProcedureRoomBuilder(){EditorApplication.delayCall+=RunPending;}
        static void RunPending()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=RunPending;return;}
            var action=File.ReadAllText(Request).Trim();File.Delete(Request);
            try
            {
                PrepareNewAssets();InspectModels();
                if(action=="build")Build();
                File.WriteAllText(Reports+"/editor-result.txt","PASS: "+action+"\n"+DateTime.Now.ToString("s"));
                Debug.Log("SHUQI_PROCEDURE_ROOM_PASS "+action);
            }
            catch(Exception error){File.WriteAllText(Reports+"/editor-result.txt","FAIL\n"+error);Debug.LogException(error);}
        }
        static void PrepareNewAssets()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Reports+"/asset_manifest.json"));
            foreach(string path in manifest.new_paths)
            {
                if(!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);Require(material,"Missing material: "+path);
                material.shader=Shader.Find("Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.3f);material.SetFloat("_Metallic",.12f);
                if(material.GetTexture("_BumpMap"))material.EnableKeyword("_NORMALMAP");
                material.enableInstancing=true;
                if(material.name.Contains("Glass"))
                {
                    material.SetColor("_BaseColor",new Color(.78f,.88f,.87f,.28f));material.SetFloat("_Surface",1);
                    material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite",0);material.SetOverrideTag("RenderType","Transparent");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        static Bounds BoundsOf(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            Require(renderers.Length>0,"No model renderers: "+obj.name);
            var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static void InspectModels()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Reports+"/asset_manifest.json"));
            var scene=EditorSceneManager.NewPreviewScene();var lines=new List<string>();
            try
            {
                foreach(var path in manifest.models.Select(n=>Imported+"/Prefabs/"+n+".prefab").Concat(new[]{PatientPath,SinkPath,NeedlePath}))
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing model: "+path);
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var b=BoundsOf(obj);
                    long triangles=obj.GetComponentsInChildren<MeshFilter>(true).Sum(m=>m.sharedMesh?Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i=>(long)m.sharedMesh.GetIndexCount(i)/3):0);
                    lines.Add(prefab.name+" | size "+b.size.ToString("F4")+" | center "+b.center.ToString("F4")+" | triangles all LODs "+triangles);
                    if(path==PatientPath||path==SinkPath||path.Contains("Light")||path.Contains("Bed"))foreach(var r in obj.GetComponentsInChildren<Renderer>(true))lines.Add("  "+r.name+" | "+r.GetType().Name+" | size "+r.bounds.size.ToString("F4")+" | center "+r.bounds.center.ToString("F4"));
                    Object.DestroyImmediate(obj);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            File.WriteAllLines(Reports+"/model-inspection.txt",lines);
        }
        static void Build()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"请先保存当前场景的未保存修改。");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            foreach(var old in scene.GetRootGameObjects().Where(o=>o.name=="ProcedureRoomRoot"||o.name=="ProcedureFillLight").ToArray())Object.DestroyImmediate(old);
            var legacy=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="LegacySceneSetup_Preserved");
            if(!legacy){legacy=new GameObject("LegacySceneSetup_Preserved");foreach(var c in scene.GetRootGameObjects().Where(o=>o.GetComponent<Camera>()).ToArray())c.transform.SetParent(legacy.transform,true);legacy.SetActive(false);}
            Directory.CreateDirectory(Art+"/Materials");AssetDatabase.Refresh();
            font=AssetDatabase.LoadAssetAtPath<Font>(Imported+"/Fonts/NotoSansSC-Regular.otf");Require(font,"Missing shared Chinese font");
            Require(font.material.shader.name!="Universal Render Pipeline/Lit","Shared font has an invalid shader");
            ink=Mat("Ink",new Color(.035f,.095f,.13f));teal=Mat("Teal",new Color(.04f,.46f,.48f));pale=Mat("Pale",new Color(.71f,.87f,.85f));
            white=Mat("White",new Color(.94f,.97f,.96f));metal=Mat("Metal",new Color(.48f,.56f,.58f));
            skin=Mat("PatientNeutral",new Color(.72f,.68f,.59f));cloth=Mat("SurgicalDrape",new Color(.20f,.47f,.48f));amber=Mat("SharpsAmber",new Color(.94f,.65f,.18f));
            var root=new GameObject("ProcedureRoomRoot");var shell=CloneHallShell(root.transform,scene);
            BuildPreparation(root.transform);
            var procedure=Group(root.transform,"B_ProcedureArea");
            var bed=Place("SM_OPR_Bed_02",procedure,new Vector3(.80f,0,.20f),0,.82f);
            var patient=BuildPatient(procedure,BoundsOf(bed).max.y);
            var back=patient.GetComponentsInChildren<Renderer>().Single(r=>r.enabled);
            var mesh=back.GetComponent<MeshFilter>();Require(mesh&&mesh.sharedMesh,"Patient mesh was not found");
            var collider=back.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;Physics.SyncTransforms();
            var patchPosition=new Vector3(.70f,back.bounds.max.y+.01f,.40f);var patchNormal=Vector3.up;
            if(collider.Raycast(new Ray(new Vector3(.70f,2,.40f),Vector3.down),out var hit,2f)){patchPosition=hit.point+hit.normal*.009f;patchNormal=hit.normal;}
            var patch=Box(procedure,"BackOperationPatch_PrimitivePlaceholder",patchPosition,new Vector3(.18f,.014f,.20f),skin);patch.transform.rotation=Quaternion.FromToRotation(Vector3.up,patchNormal);
            Shape(patch.transform,"LesionMarker_PrimitivePlaceholder",PrimitiveType.Sphere,new Vector3(0,.75f,0),new Vector3(.17f,1.5f,.16f),skin);
            foreach(string name in new[]{"IncisionStateAnchor","LipomaStateAnchor","ClosureStateAnchor","DressingStateAnchor"})Group(patch.transform,name);
            BuildBodyCover(procedure,collider,BoundsOf(bed).max.y);
            var pillow=Shape(procedure,"HeadSupport_PrimitivePlaceholder",PrimitiveType.Cube,new Vector3(.80f,.90f,.80f),new Vector3(.30f,.16f,.28f),pale);
            BuildLight(procedure);
            var checklist=Panel(root.transform,"BedsideChecklist_StaticUI",new Vector3(.20f,1.94f,3.15f),new Vector2(2.18f,.94f),"小手术操作训练","S05–S58 · 标准病例 LIP-UB-01","术前准备   /   切除与关闭   /   术后交接",0);
            BuildInstrumentAndHandoff(root.transform);
            BuildNurseAndSpecimen(root.transform);
            BuildChartAndExit(root.transform);
            var stands=Group(root.transform,"G_PlayerStandpoints");
            Standpoint(stands,"Spawn_Chart",new Vector3(2.70f,0,1.17f),0,"A 病历观察位");
            Standpoint(stands,"Spawn_Preparation",new Vector3(-2.73f,0,-.05f),0,"C 备物与穿戴");
            Standpoint(stands,"Spawn_HandHygiene",new Vector3(-2.73f,0,1.95f),0,"C 手卫生位");
            Standpoint(stands,"Spawn_Operator",new Vector3(.02f,0,.40f),90,"B 床旁操作位");
            Standpoint(stands,"Spawn_Exit",new Vector3(2.52f,0,-2.12f),180,"F 离室交接位");
            var rig=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="QuestRig");
            if(!rig)rig=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/QuestRig.prefab"),scene);
            Require(rig,"Missing QuestRig prefab");rig.transform.SetPositionAndRotation(new Vector3(2.70f,0,1.17f),Quaternion.identity);
            foreach(var r in rig.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            var xrCamera=rig.GetComponentInChildren<Camera>(true);xrCamera.nearClipPlane=.04f;xrCamera.farClipPlane=30;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.70f,.76f,.78f);RenderSettings.skybox=null;RenderSettings.fog=false;
            var sun=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun){sun.intensity=.75f;sun.color=new Color(1,.98f,.95f);sun.shadows=LightShadows.None;sun.transform.rotation=Quaternion.Euler(45,-30,0);}
            var fill=new GameObject("ProcedureFillLight").AddComponent<Light>();fill.transform.position=new Vector3(.2f,2.7f,.4f);fill.type=LightType.Point;fill.range=9;fill.intensity=.9f;fill.shadows=LightShadows.None;
            AssetDatabase.SaveAssets();Canvas.ForceUpdateCanvases();Validate(scene,patchPosition);
            Require(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save operation room");ExportViews(scene,shell);
            foreach(var t in shell.GetComponentsInChildren<Transform>())if(t.name.StartsWith("Ceiling")||t.name.StartsWith("SouthWall_")||t.name.StartsWith("Door"))SceneVisibilityManager.instance.Hide(t.gameObject,true);
            Selection.activeGameObject=root;if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(new Vector3(0,1,.4f),Quaternion.Euler(40,-20,0),4.2f);
        }
        static void BuildPreparation(Transform parent)
        {
            var prep=Group(parent,"C_PreparationArea");
            var partition=Group(prep,"PreparationScreen_PrimitivePlaceholder");
            foreach(float z in new[]{-2.15f,2.48f})
            {
                Box(partition,"ScreenPanel",new Vector3(-1.70f,1.05f,z),new Vector3(.065f,2.10f,1.35f),pale);
                Box(partition,"ScreenBase",new Vector3(-1.70f,.04f,z),new Vector3(.35f,.08f,1.40f),metal);
            }
            BuildSinkPlaceholder(prep);
            var counter=Place("SM_OPR_Table_02",prep,new Vector3(-2.73f,0,.70f),90,.88f);float top=BoundsOf(counter).max.y;
            Place("SM_OPR_Gauze_Box",prep,new Vector3(-3.02f,top+.005f,.78f),0);
            Place("SM_OPR_Bottle_Disinfectant",prep,new Vector3(-2.44f,top+.005f,.78f),0);
            Box(prep,"GownPack_PrimitivePlaceholder",new Vector3(-2.73f,top+.035f,.62f),new Vector3(.22f,.055f,.19f),cloth,false);
            Box(prep,"GlovePack_PrimitivePlaceholder",new Vector3(-2.46f,top+.013f,.57f),new Vector3(.18f,.02f,.15f),white,false);
            Place("SM_OPR_Cabinet_01_Full",prep,new Vector3(-2.73f,0,-1.75f),90,1.20f);
            Panel(prep,"PreparationGuide_StaticUI",new Vector3(-2.73f,1.85f,3.15f),new Vector2(1.35f,.47f),"C 准备区","S13–S16 / S52","包装检查 · 着装 · 手卫生",0);
        }
        static GameObject BuildPatient(Transform parent,float bedTop)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PatientPath);Require(prefab,"Missing prone patient");
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);obj.name="Patient_Prone_SourceModel";
            var parts=obj.GetComponentsInChildren<Renderer>(true);var body=parts.OrderByDescending(r=>r.bounds.size.x).First();
            foreach(var part in parts){part.enabled=part==body;part.sharedMaterials=Enumerable.Repeat(skin,Mathf.Max(1,part.sharedMaterials.Length)).ToArray();}
            obj.transform.rotation=Quaternion.Euler(0,270,0);var b=BoundsOf(obj);obj.transform.localScale*=1.75f/b.size.z;
            b=BoundsOf(obj);obj.transform.position+=new Vector3(.80f,bedTop-.003f,.20f)-new Vector3(b.center.x,b.min.y,b.center.z);
            return obj;
        }
        static void BuildBodyCover(Transform parent,MeshCollider patient,float bedTop)
        {
            const int columns=13,rows=49;var vertices=new Vector3[columns*rows];var triangles=new List<int>();
            for(int row=0;row<rows;row++)
            {
                float z=Mathf.Lerp(-.87f,.25f,row/(float)(rows-1)),y=bedTop+.025f;
                for(int sample=0;sample<=32;sample++)if(patient.Raycast(new Ray(new Vector3(.38f+.84f*sample/32f,2,z),Vector3.down),out var hit,2f))y=Mathf.Max(y,hit.point.y+.035f);
                for(int column=0;column<columns;column++)
                {
                    float x=.80f+Mathf.Lerp(-.42f,.42f,column/(float)(columns-1));int index=row*columns+column;vertices[index]=new Vector3(x,y,z);
                    if(row<rows-1&&column<columns-1)triangles.AddRange(new[]{index,index+columns,index+1,index+1,index+columns,index+columns+1});
                }
            }
            string path=Art+"/LowerBodyCover.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name="LowerBodyCover";mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            Require(mesh.vertexCount==columns*rows&&mesh.subMeshCount==1,"Invalid cover mesh");
            var obj=new GameObject("LowerBodyCover_StaticPlaceholder",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=cloth;
        }
        static void BuildSinkPlaceholder(Transform parent)
        {
            var sink=Group(parent,"SurgicalSink_PrimitivePlaceholder");
            Box(sink,"Base",new Vector3(-2.73f,.40f,2.78f),new Vector3(1.32f,.80f,.46f),metal);
            Box(sink,"Splashback",new Vector3(-2.73f,1.02f,3.015f),new Vector3(1.38f,.45f,.035f),metal,false);
            Box(sink,"FrontRim",new Vector3(-2.73f,.88f,2.49f),new Vector3(1.38f,.10f,.06f),white,false);
            Box(sink,"BackRim",new Vector3(-2.73f,.88f,2.99f),new Vector3(1.38f,.10f,.06f),white,false);
            foreach(float x in new[]{-3.39f,-2.73f,-2.07f})Box(sink,"SideRim",new Vector3(x,.88f,2.74f),new Vector3(.06f,.10f,.52f),white,false);
            foreach(float x in new[]{-3.06f,-2.40f})
            {
                Box(sink,"BasinBottom",new Vector3(x,.815f,2.74f),new Vector3(.59f,.02f,.43f),ink,false);
                Shape(sink,"TapStem",PrimitiveType.Cylinder,new Vector3(x,1.05f,2.96f),new Vector3(.035f,.12f,.035f),metal);
                Shape(sink,"TapSpout",PrimitiveType.Cube,new Vector3(x,1.16f,2.85f),new Vector3(.045f,.045f,.23f),metal);
            }
        }
        static void BuildLight(Transform parent)
        {
            var light=Group(parent,"SurgicalLight_SourceAndPrimitiveSupport");
            Place("SM_OPR_Surgical_Light_01",light,new Vector3(.80f,2.10f,.40f),0,.25f);
            Box(light,"CeilingMount_PrimitivePlaceholder",new Vector3(.80f,3.02f,.40f),new Vector3(.34f,.12f,.34f),white,false);
            Shape(light,"SupportStem",PrimitiveType.Cylinder,new Vector3(.80f,2.70f,.40f),new Vector3(.045f,.30f,.045f),metal);
            Shape(light,"Joint",PrimitiveType.Sphere,new Vector3(.80f,2.37f,.40f),Vector3.one*.13f,metal);
        }
        static void BuildInstrumentAndHandoff(Transform parent)
        {
            var cart=Group(parent,"D_InstrumentCart");
            var table=Place("SM_OPR_Table_02",cart,new Vector3(-.37f,0,1.52f),90,.86f);float top=BoundsOf(table).max.y+.01f;
            Box(cart,"SterileTableCover_PrimitivePlaceholder",new Vector3(-.37f,top,1.52f),new Vector3(.84f,.012f,.54f),cloth,false);
            Place("SM_OPR_Tray_01",cart,new Vector3(-.60f,top+.01f,1.50f),0);
            var scalpel=Group(cart,"ScalpelAssembly_SourceModels");
            Place("SM_OPR_Scalpel_Handle_01",scalpel,new Vector3(-.12f,top+.025f,1.36f),0);
            Place("SM_OPR_Scalpel_01",scalpel,new Vector3(-.035f,top+.025f,1.36f),0);
            Place("SM_OPR_Thumb_Tissue_Forceps_01",cart,new Vector3(-.37f,top+.025f,1.36f),0);
            Place("SM_OPR_Surgical_Scissors_01",cart,new Vector3(-.10f,top+.025f,1.53f),0);
            Place("SM_OPR_Needle_Holder_01",cart,new Vector3(-.10f,top+.025f,1.70f),0);
            Place("SM_OPR_Forceps_01",cart,new Vector3(-.57f,top+.055f,1.52f),0);
            Place("SM_OPR_Gauze",cart,new Vector3(-.63f,top+.025f,1.72f),0);
            var needle=PlaceRaw(NeedlePath,cart,new Vector3(-.33f,top+.025f,1.68f),0,.025f);
            foreach(var r in needle.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(metal,r.sharedMaterials.Length).ToArray();
            Place("SM_OPR_Bottle_Medicine_01",cart,new Vector3(-.66f,top+.01f,1.35f),0);
            BuildSyringe(cart,new Vector3(-.36f,top+.03f,1.75f));
            var count=Panel(cart,"CountBoard_StaticUI",new Vector3(-.40f,1.18f,1.94f),new Vector2(.68f,.32f),"D 器械与清点","S17 / S39 / S47","器械 · 纱布 · 锐器",0);count.rotation=Quaternion.Euler(0,0,0);
            cart.position+=new Vector3(0,0,-.35f);
            var handoff=Group(parent,"E_HandoffAndSharps");
            var h=Place("SM_OPR_Table_03",handoff,new Vector3(-.32f,0,-.22f),0,.83f);float ht=BoundsOf(h).max.y+.01f;
            Place("SM_OPR_Tray_01",handoff,new Vector3(-.20f,ht,.01f),0);
            Box(handoff,"SharpsContainer_PrimitivePlaceholder",new Vector3(-.51f,ht+.10f,-.34f),new Vector3(.24f,.20f,.20f),amber);
            Box(handoff,"SharpsLid",new Vector3(-.51f,ht+.205f,-.34f),new Vector3(.25f,.025f,.21f),ink,false);
            Box(handoff,"SharpsSlot",new Vector3(-.51f,ht+.22f,-.34f),new Vector3(.10f,.004f,.025f),amber,false);
            Panel(handoff,"HandoffLabel_StaticUI",new Vector3(-.80f,1.14f,-.22f),new Vector2(.62f,.25f),"E 交接盘 / 锐器盒","S24 / S36 / S51","分区放置 · 护士配合",-90);
        }
        static void BuildSyringe(Transform parent,Vector3 position)
        {
            var obj=Group(parent,"Syringe_PrimitivePlaceholder");obj.position=position;obj.localRotation=Quaternion.Euler(0,0,90);
            Shape(obj,"Barrel",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.018f,.04f,.018f),pale);
            Shape(obj,"Plunger",PrimitiveType.Cylinder,new Vector3(0,.065f,0),new Vector3(.01f,.025f,.01f),white);
            Shape(obj,"Flange",PrimitiveType.Cube,new Vector3(0,.04f,0),new Vector3(.045f,.005f,.018f),white);
            Shape(obj,"Needle",PrimitiveType.Cylinder,new Vector3(0,-.058f,0),new Vector3(.002f,.018f,.002f),metal);
        }
        static void BuildNurseAndSpecimen(Transform parent)
        {
            var zone=Group(parent,"NurseAndSpecimenStation");var nurse=Group(zone,"Nurse_PrimitivePlaceholder");nurse.position=new Vector3(1.70f,0,.75f);nurse.rotation=Quaternion.Euler(0,270,0);
            Shape(nurse,"Body",PrimitiveType.Capsule,new Vector3(0,1.04f,0),new Vector3(.36f,.30f,.24f),cloth);
            Shape(nurse,"Head",PrimitiveType.Sphere,new Vector3(0,1.49f,0),Vector3.one*.23f,skin);
            Shape(nurse,"Cap",PrimitiveType.Cube,new Vector3(0,1.59f,0),new Vector3(.24f,.075f,.20f),white);
            for(int i=-1;i<=1;i+=2)
            {
                Shape(nurse,"Leg_"+i,PrimitiveType.Capsule,new Vector3(i*.095f,.40f,0),new Vector3(.13f,.36f,.13f),cloth);
                Shape(nurse,"Shoe_"+i,PrimitiveType.Cube,new Vector3(i*.095f,.055f,.045f),new Vector3(.15f,.10f,.25f),white);
                Shape(nurse,"Arm_"+i,PrimitiveType.Capsule,new Vector3(i*.26f,1.0f,.04f),new Vector3(.095f,.25f,.095f),cloth);
            }
            Panel(zone,"NurseLabel",new Vector3(1.70f,1.90f,.75f),new Vector2(.80f,.22f),"护士站位","虚拟配合角色占位","",90);
            var table=Place("SM_OPR_Table_03",zone,new Vector3(2.82f,0,.10f),0,.88f);float top=BoundsOf(table).max.y;
            Place("SM_OPR_Tray_01",zone,new Vector3(2.82f,top+.01f,.10f),0);
            Shape(zone,"SpecimenContainer_PrimitivePlaceholder",PrimitiveType.Cylinder,new Vector3(2.82f,top+.09f,.10f),new Vector3(.11f,.075f,.11f),pale);
            Shape(zone,"SpecimenLid",PrimitiveType.Cylinder,new Vector3(2.82f,top+.168f,.10f),new Vector3(.12f,.012f,.12f),teal);
            Box(zone,"SpecimenLabel_PrimitivePlaceholder",new Vector3(2.82f,top+.08f,.038f),new Vector3(.07f,.05f,.005f),white,false);
            Panel(zone,"SpecimenStation_StaticUI",new Vector3(3.52f,1.42f,.20f),new Vector2(.90f,.43f),"标本封装台","S36 · 护士侧核对","模拟容器 · 标签待配置",90);
            var bin=Place("SM_OPR_Biohazard_Bin_01_Main",zone,new Vector3(2.94f,0,-1.10f),0,.56f);
            var cap=Place("SM_OPR_Biohazard_Bin_01_Cap",zone,new Vector3(2.94f,BoundsOf(bin).max.y,-1.10f),0,.053f);
            Box(zone,"GeneralWaste_PrimitivePlaceholder",new Vector3(2.36f,.26f,-1.10f),new Vector3(.37f,.52f,.36f),metal);
            Box(zone,"GeneralWasteLid",new Vector3(2.36f,.53f,-1.10f),new Vector3(.39f,.035f,.38f),ink,false);
        }
        static void BuildChartAndExit(Transform parent)
        {
            var chart=Group(parent,"A_ChartTerminal");
            Box(chart,"ChartCounter_PrimitivePlaceholder",new Vector3(2.70f,.89f,2.48f),new Vector3(1.12f,.06f,.57f),white);
            Box(chart,"ChartCounterBase",new Vector3(2.70f,.44f,2.58f),new Vector3(.86f,.88f,.33f),metal);
            var screen=Place("SM__TVScreen",chart,new Vector3(2.70f,.92f,2.48f),0,.46f);var sb=BoundsOf(screen);
            var canvas=CanvasAt("ChartCase_StaticUI",chart,new Vector3(sb.center.x,sb.center.y,sb.min.z-.008f),new Vector2(sb.size.x*.88f,sb.size.y*.84f));
            Image(canvas,"Page",Vector2.zero,((RectTransform)canvas).sizeDelta,new Color(.92f,.96f,.95f));
            Text(canvas,"Title","A 病历与计划",new Vector2(0,105),new Vector2(600,90),45,new Color(.04f,.29f,.31f));
            Text(canvas,"Case","LIP-UB-01 · 上背部浅表脂肪瘤\nS05 病例阅读 / S57 记录交接\n模拟病例 · 医学内容待审核",new Vector2(0,-35),new Vector2(600,180),27,new Color(.12f,.24f,.27f));
            var exit=Group(parent,"F_ExitHandover");
            Panel(exit,"ExitPanel_StaticUI",new Vector3(1.08f,1.50f,-2.88f),new Vector2(.85f,.56f),"F 离室交接","S58 → S59 复诊室","完成交接后时间跳转",180);
        }
        static Transform CloneHallShell(Transform parent,Scene target)
        {
            var hall=EditorSceneManager.OpenScene(HallPath,OpenSceneMode.Additive);
            try
            {
                var source=hall.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="01_RoomShell_PrimitivePlaceholder");
                var obj=Object.Instantiate(source.gameObject);SceneManager.MoveGameObjectToScene(obj,target);obj.name="RoomShell_PrimitivePlaceholder";
                obj.transform.SetParent(parent,false);obj.transform.localScale=new Vector3(1.18f,1,1);return obj.transform;
            }
            finally{EditorSceneManager.CloseScene(hall,true);}
        }
        static GameObject Place(string name,Transform parent,Vector3 bottom,float yaw,float height=-1)
        {
            var obj=PlaceRaw(Imported+"/Prefabs/"+name+".prefab",parent,bottom,yaw,height);
            if(BoundsOf(obj).size.magnitude<.5f)foreach(var group in obj.GetComponentsInChildren<LODGroup>())
            {var lods=group.GetLODs();if(lods.Length>0){group.SetLODs(new[]{new LOD(0f,lods[0].renderers)});group.RecalculateBounds();}}
            return obj;
        }
        static GameObject PlaceRaw(string path,Transform parent,Vector3 bottom,float yaw,float height)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing model: "+path);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);obj.transform.rotation=Quaternion.Euler(0,yaw,0);var b=BoundsOf(obj);
            if(height>0){Require(b.size.y>.0001f,"Invalid model bounds: "+path);obj.transform.localScale*=height/b.size.y;b=BoundsOf(obj);}
            obj.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);return obj;
        }
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Material Mat(string name,Color color)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
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
            var obj=new GameObject(name,typeof(RectTransform),typeof(Canvas));obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=Vector3.one*.001f;
            ((RectTransform)obj.transform).sizeDelta=meters*1000;obj.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;return obj.transform;
        }
        static void Image(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchoredPosition=pos;rect.sizeDelta=size;var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
        }
        static void Text(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchoredPosition=pos;rect.sizeDelta=size;
            var label=obj.GetComponent<UnityEngine.UI.Text>();label.font=font;label.fontSize=fontSize;label.text=value;label.color=color;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
        }
        static Transform Panel(Transform parent,string name,Vector3 position,Vector2 size,string title,string context,string footer,float yaw)
        {
            var panel=Group(parent,name);panel.position=position;panel.rotation=Quaternion.Euler(0,yaw,0);
            var frame=Shape(panel,"Frame",PrimitiveType.Cube,Vector3.zero,new Vector3(size.x+.03f,size.y+.03f,.025f),ink);
            var canvas=CanvasAt("Canvas",panel,position+panel.rotation*new Vector3(0,0,-.015f),size);canvas.rotation=panel.rotation;
            Image(canvas,"Background",Vector2.zero,size*1000,new Color(.035f,.095f,.13f));
            Text(canvas,"Title",title,new Vector2(0,size.y*230),new Vector2(size.x*920,size.y*320),Mathf.RoundToInt(size.y*112),Color.white);
            Text(canvas,"Context",context,new Vector2(0,-size.y*40),new Vector2(size.x*940,size.y*230),Mathf.RoundToInt(size.y*67),new Color(.66f,.86f,.85f));
            Text(canvas,"Footer",footer,new Vector2(0,-size.y*310),new Vector2(size.x*940,size.y*220),Mathf.RoundToInt(size.y*60),new Color(.66f,.86f,.85f));return panel;
        }
        static void Standpoint(Transform parent,string name,Vector3 position,float yaw,string label)
        {
            var anchor=Group(parent,name);anchor.position=position;anchor.rotation=Quaternion.Euler(0,yaw,0);
            Shape(anchor,"FloorMarker",PrimitiveType.Cube,new Vector3(0,.008f,0),new Vector3(.63f,.009f,.58f),pale);
            var canvas=CanvasAt(name+"Label",anchor,position+new Vector3(0,.017f,0),new Vector2(.58f,.18f));canvas.rotation=Quaternion.Euler(90,yaw,0);
            Text(canvas,"Text",label,Vector2.zero,new Vector2(580,160),48,new Color(.04f,.29f,.31f));
        }
        static void Validate(Scene scene,Vector3 patch)
        {
            var objects=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            Require(objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount)==0,"Missing scene scripts");
            Require(objects.SelectMany(o=>o.GetComponents<Camera>()).Count(c=>c.isActiveAndEnabled)==1,"Expected one active XR camera");
            Require(objects.SelectMany(o=>o.GetComponents<AudioListener>()).Count(l=>l.isActiveAndEnabled)==1,"Expected one active audio listener");
            var renderers=objects.Where(o=>o.activeInHierarchy).SelectMany(o=>o.GetComponents<Renderer>()).Where(r=>r.enabled).ToArray();
            Require(renderers.All(r=>r.sharedMaterials.Length>0&&r.sharedMaterials.All(m=>m&&m.shader&&m.shader.name!="Hidden/InternalErrorShader")),"Invalid scene material or shader");
            foreach(string name in new[]{"Spawn_Chart","Spawn_Preparation","Spawn_HandHygiene","Spawn_Operator","Spawn_Exit","BackOperationPatch_PrimitivePlaceholder","Nurse_PrimitivePlaceholder"})Require(objects.Any(o=>o.name==name),"Missing zone: "+name);
            float reach=Vector2.Distance(new Vector2(.02f,.40f),new Vector2(patch.x,patch.z));Require(reach<.75f,"Operation patch is too far from the operator anchor");
            File.WriteAllText(Reports+"/scene-validation.txt","PASS\nScene: "+ScenePath+"\nActive renderers: "+renderers.Length+"\nActive XR cameras: 1\nMissing scripts: 0\nOperation patch: "+patch.ToString("F3")+"\nOperator horizontal reach: "+reach.ToString("F3")+" m\nScope: S05-S58 environment and static UI only.\nQuest 3 hardware: NOT TESTED\n");
        }
        static void ExportViews(Scene scene,Transform shell)
        {
            var go=new GameObject("ProcedurePreviewCamera_EditorOnly");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=35;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.86f,.85f);var roof=shell.Find("Ceiling_CutawayForPreview");
            var lamp=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>()).Single(t=>t.name=="SurgicalLight_SourceAndPrimitiveSupport");
            try
            {
                Render(camera,new Vector3(-1.38f,2.04f,-1.9f),new Vector3(.74f,1.1f,.55f),false,"procedure-overview.png");
                Render(camera,new Vector3(.02f,1.64f,.40f),new Vector3(.78f,1.1f,.40f),false,"procedure-operator-view.png");
                Render(camera,new Vector3(-2.73f,1.64f,-.15f),new Vector3(-2.73f,1.0f,1.85f),false,"procedure-preparation-view.png");
                roof.gameObject.SetActive(false);lamp.gameObject.SetActive(false);Render(camera,new Vector3(0,10,.15f),new Vector3(0,0,.151f),true,"procedure-top-view.png");
            }
            finally{roof.gameObject.SetActive(true);lamp.gameObject.SetActive(true);Object.DestroyImmediate(go);}
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static void Render(Camera camera,Vector3 position,Vector3 target,bool ortho,string name)
        {
            camera.transform.position=position;camera.transform.LookAt(target);camera.orthographic=ortho;camera.orthographicSize=3.55f;camera.fieldOfView=74;
            var rt=new RenderTexture(1800,1250,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1800,1250,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1800,1250),0,0);texture.Apply();File.WriteAllBytes(Reports+"/"+name,texture.EncodeToPNG());Object.DestroyImmediate(texture);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
