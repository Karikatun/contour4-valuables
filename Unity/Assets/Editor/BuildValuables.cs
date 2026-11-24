using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildValuables
{
    [Serializable] public class Document { public string key; public Part[] parts; public Shape[] shapes,shapeCandidates; public int textureSize; public float[] size; public float mass,normalOrientationScore; public Probe[] clearProbes; }
    [Serializable] public class Part { public string name; public bool glass; public float[] vertices,normals,uv; public int[] indices; public Surface material; }
    [Serializable] public class Surface { public string key,colorTexture,normalTexture,metalTexture; public float[] color; public float metallic,smoothness; }
    [Serializable] public class Probe { public string name; public float[] position; }
    [Serializable] public class Shape { public string name; public float[] center,size; }
    [Serializable] public class Report { public string unity; public bool success; public Item[] items; }
    [Serializable] public class Item { public string key; public Vector3 size; public int triangles,renderers,colliders; public bool glass; public float normalOrientationScore,settledBottom,settledSpeed,mass; public bool settled,contactFit; public string[] clearProbes; public string materialShader; public string[] packedTextures; public float packedPositionError,packedNormalError; }
    static readonly Vector3[] Sizes={new Vector3(.24f,.08f,.16f),new Vector3(.12f,.30f,.12f),new Vector3(.22f,1.65f,.22f),new Vector3(.33f,.30f,.30f),new Vector3(.46f,.34f,.32f),new Vector3(1.18f,.76f,.55f),new Vector3(1.35f,.95f,.52f),new Vector3(.52f,.43f,.22f),new Vector3(1.7059345f,.9693324f,1.55f)};
    static readonly float[] Masses={.8f,.7f,2.4f,9f,3.2f,8f,14f,2.1f,36f};
    static readonly string[] OriginalKeys={"experiment-cassette","control-sample","sensor-rod","calibration-weight","portable-gravimeter","gimbal-gyroscope","gravity-lens","mobius-strip","field-core"};
    static Vector3 Size(Document doc){int i=Array.IndexOf(OriginalKeys,doc.key);return i>=0?Sizes[i]:V(doc.size);}
    static readonly Dictionary<string,float> AddedMasses=new Dictionary<string,float>{{"anomaly-cryostat",22},{"corner-cube",.25f},{"free-fall-photogate",.65f},{"pirani-gauge-head",.4f},{"bullseye-level",.18f},{"atwood-machine",4.5f},{"kater-pendulum",5.5f},{"vertical-seismometer",3.2f},{"hydrostatic-balance",3.8f},{"vacuum-drop-column",18},{"cavendish-balance",12}};
    static float Mass(Document doc){int i=Array.IndexOf(OriginalKeys,doc.key);return i>=0?Masses[i]:AddedMasses[doc.key];}
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
    public static void AuditPacked(){
        try{
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bundle=AssetBundle.LoadFromFile(Root+"/dist/bundles/contour4-valuables.bundle");if(!bundle)throw new Exception("Packed audit load failed");
            var items=new List<Item>();int index=0;
            foreach(var folder in Directory.GetDirectories("Assets/Source").OrderBy(x=>x)){
                var doc=JsonUtility.FromJson<Document>(File.ReadAllText(folder+"/mesh.json"));
                foreach(var label in new[]{"color","normal","metal"}){string path=folder+"/"+label+".png";if(!File.Exists(path)){if(doc.parts.Any(p=>p.material==null))throw new Exception("Missing atlas "+path);continue;}var importer=(TextureImporter)AssetImporter.GetAtPath(path);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    int expected=doc.textureSize>0?doc.textureSize:512;
                    if(texture.width!=expected||texture.height!=expected||importer.sRGBTexture!=(label=="color")||((importer.textureType==TextureImporterType.NormalMap)!=(label=="normal"))||importer.flipGreenChannel)throw new Exception("Texture flag/dimensions mismatch "+path);
                }
                var prefab=bundle.LoadAsset<GameObject>("assets/generated/"+doc.key+"/"+doc.key+".prefab");if(!prefab)throw new Exception("Packed prefab missing "+doc.key);var root=UnityEngine.Object.Instantiate(prefab);var meshes=root.GetComponentsInChildren<MeshFilter>();var bounds=meshes[0].sharedMesh.bounds;foreach(var m in meshes)bounds.Encapsulate(m.sharedMesh.bounds);
                if(Vector3.Distance(bounds.size,Size(doc))>.0001f||Mathf.Abs(bounds.min.y)>.0001f)throw new Exception("Packed dimensions/pivot mismatch "+doc.key);
                var entry=new Item{key=doc.key,size=bounds.size,triangles=meshes.Sum(m=>m.sharedMesh.triangles.Length/3),renderers=meshes.Length,colliders=root.GetComponentsInChildren<Collider>().Length,glass=doc.parts.Any(p=>p.glass),normalOrientationScore=Orientation(doc)};
                if(doc.key=="field-core"){
                    var mesh=meshes.Single().sharedMesh;var source=doc.parts.Single();var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                    if(vertices.Length*3!=source.vertices.Length||!mesh.triangles.SequenceEqual(source.indices))throw new Exception("Packed core topology changed");
                    for(int j=0;j<vertices.Length;j++){
                        entry.packedPositionError=Mathf.Max(entry.packedPositionError,Vector3.Distance(vertices[j],new Vector3(source.vertices[j*3],source.vertices[j*3+1],source.vertices[j*3+2])));
                        entry.packedNormalError=Mathf.Max(entry.packedNormalError,Vector3.Distance(normals[j],new Vector3(source.normals[j*3],source.normals[j*3+1],source.normals[j*3+2])));
                        if(normals[j].sqrMagnitude<.99f||Vector2.Distance(uv[j],new Vector2(source.uv[j*2],source.uv[j*2+1]))>.000001f)throw new Exception("Packed core normal or UV regression");
                    }
                    if(entry.packedPositionError>.000001f||entry.packedNormalError>.000001f)throw new Exception("Packed core geometry transfer changed");
                    var mat=root.GetComponentInChildren<MeshRenderer>().sharedMaterial;entry.materialShader=mat.shader.name;
                    if(mat.shader.name!="Standard"||!mat.IsKeywordEnabled("_NORMALMAP")||!mat.IsKeywordEnabled("_METALLICGLOSSMAP"))throw new Exception("Core shader/keywords missing from bundle");
                    var textureRows=new List<string>();
                    foreach(var role in new[]{"_MainTex","_BumpMap","_MetallicGlossMap"}){
                        var texture=mat.GetTexture(role);if(!texture||texture.width!=1024||texture.height!=1024||texture.isDataSRGB!=(role=="_MainTex"))throw new Exception("Packed core texture mismatch "+role);
                        textureRows.Add(role+":1024x1024:"+(texture.isDataSRGB?"sRGB":"linear")+":"+texture.graphicsFormat);
                    }
                    entry.packedTextures=textureRows.ToArray();
                }
                Settle(root,entry,doc);ContactChecks(root,bounds,entry);Clearance(root,entry,doc);AuditMaterials(root,entry,doc,folder);AuditGeometry(root,entry,doc);if(!OriginalKeys.Contains(doc.key)&&doc.parts.Any(p=>p.glass))TransmissionProbe(root,bounds,doc);items.Add(entry);UnityEngine.Object.DestroyImmediate(root);index++;
            }
            bundle.Unload(false);if(items.Count!=20)throw new Exception("Packed count mismatch");File.WriteAllText(Root+"/evidence/unity-packed-audit.json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,success=true,items=items.ToArray()},true));Debug.Log("CONTOUR4_PACKED_AUDIT_OK 20 prefabs, material/texture flags, geometry, contact-fit, mass and settling");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Build()
    {
        try
        {
            Directory.CreateDirectory("Assets/Generated"); AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var items=new List<Item>(); var paths=new List<string>(); int index=0;
            foreach(string folder in Directory.GetDirectories("Assets/Source").OrderBy(x=>x))
            {
                var doc=JsonUtility.FromJson<Document>(File.ReadAllText(folder+"/mesh.json"));
                // Preserve validated 0.1.0 prefab, mesh, material and texture identities.
                if(OriginalKeys.Contains(doc.key)){
                    string oldPath="Assets/Generated/"+doc.key+"/"+doc.key+".prefab";
                    var original=AssetDatabase.LoadAssetAtPath<GameObject>(oldPath);if(!original)throw new Exception("Missing preserved prefab "+doc.key);
                    var clone=UnityEngine.Object.Instantiate(original);var meshes=clone.GetComponentsInChildren<MeshFilter>();var b=meshes[0].sharedMesh.bounds;foreach(var m in meshes)b.Encapsulate(m.sharedMesh.bounds);
                    if(Vector3.Distance(b.size,Size(doc))>.0001f||Mathf.Abs(b.min.y)>.0001f)throw new Exception("Preserved dimensions drift "+doc.key);
                    var oldEntry=new Item{key=doc.key,size=b.size,triangles=meshes.Sum(m=>m.sharedMesh.triangles.Length/3),colliders=clone.GetComponentsInChildren<Collider>().Length,renderers=meshes.Length,glass=doc.parts.Any(p=>p.glass),normalOrientationScore=Orientation(doc)};
                    Settle(clone,oldEntry,doc);ContactChecks(clone,b,oldEntry);items.Add(oldEntry);paths.Add(oldPath);UnityEngine.Object.DestroyImmediate(clone);index++;continue;
                }
                string output="Assets/Generated/"+doc.key;Directory.CreateDirectory(output);AssetDatabase.Refresh();
                int textureSize=doc.textureSize>0?doc.textureSize:512;
                var color=File.Exists(folder+"/color.png")?Texture(folder+"/color.png",true,false,textureSize):null;var normal=File.Exists(folder+"/normal.png")?Texture(folder+"/normal.png",false,true,textureSize):null;var metal=File.Exists(folder+"/metal.png")?Texture(folder+"/metal.png",false,false,textureSize):null;
                var opaque=new Material(Shader.Find("Standard")){name=doc.key+" baked analog apparatus",color=Color.white};
                opaque.SetTexture("_MainTex",color);opaque.SetTexture("_BumpMap",normal);opaque.EnableKeyword("_NORMALMAP");opaque.SetFloat("_BumpScale",1);
                opaque.SetTexture("_MetallicGlossMap",metal);opaque.SetFloat("_GlossMapScale",1);opaque.EnableKeyword("_METALLICGLOSSMAP");
                AssetDatabase.CreateAsset(opaque,output+"/Opaque.mat");
                var glass=new Material(Shader.Find("Standard")){name=doc.key+" clear glass",color=new Color(.82f,.94f,.98f,.12f)};
                glass.SetFloat("_Mode",3);glass.SetFloat("_Glossiness",.94f);glass.SetFloat("_Metallic",0);glass.SetOverrideTag("RenderType","Transparent");
                glass.SetInt("_SrcBlend",(int)BlendMode.One);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");glass.renderQueue=3000;
                AssetDatabase.CreateAsset(glass,output+"/Glass.mat");
                var root=new GameObject(doc.key);var visual=new GameObject("Visuals");visual.transform.SetParent(root.transform,false);
                int triangles=0;float score=1;
                // Preserve authored material identities, including cryostat patches and glass tints.
                var surfaces=new Dictionary<string,Material>();
                foreach(var p in MergedParts(doc))
                {
                    bool isGlass=p.glass;string id=p.material!=null?p.material.key:(isGlass?"glass":"opaque");
                    if(!surfaces.TryGetValue(id,out var material)){
                        material=p.material!=null?SurfaceMaterial(p.material,isGlass,folder,textureSize):new Material(isGlass?glass:opaque);
                        material.name=doc.key+" "+id;surfaces.Add(id,material);AssetDatabase.CreateAsset(material,output+"/Material-"+surfaces.Count+".mat");
                    }
                    var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();
                    for(int j=0;j<p.vertices.Length;j+=3){v.Add(new Vector3(p.vertices[j],p.vertices[j+1],p.vertices[j+2]));n.Add(new Vector3(p.normals[j],p.normals[j+1],p.normals[j+2]));}
                    for(int j=0;j<p.uv.Length;j+=2)uv.Add(new Vector2(p.uv[j],p.uv[j+1]));
                    var mesh=new Mesh{name=p.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(p.indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
                    triangles+=p.indices.Length/3;AssetDatabase.CreateAsset(mesh,output+"/Part-"+triangles+".asset");var part=new GameObject(p.name);part.transform.SetParent(visual.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=isGlass?ShadowCastingMode.Off:ShadowCastingMode.On;
                }
                score=Orientation(doc);
                // Primitive compounds follow the body, grip and end-head shapes. No dynamic triangle mesh.
                var colliders=new GameObject("Hitboxes");colliders.transform.SetParent(root.transform,false);
                AddColliders(doc,colliders.transform);
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>())Debug.Log("BOUNDS_PROBE "+doc.key+" "+r.name+" renderer="+r.bounds+" mesh="+r.GetComponent<MeshFilter>().sharedMesh.bounds+" active="+r.enabled);
                Bounds bounds=root.GetComponentsInChildren<MeshFilter>().First().sharedMesh.bounds;foreach(var m in root.GetComponentsInChildren<MeshFilter>())bounds.Encapsulate(m.sharedMesh.bounds);
                if(Vector3.Distance(bounds.size,Size(doc))>.0001f)throw new Exception(doc.key+" wrong imported dimensions "+bounds.size);
                if(Mathf.Abs(bounds.min.y)>.0001f)throw new Exception("Bottom pivot drift: "+doc.key);
                // Approved Meshy source has 254 opposing averaged corner normals out of 14944.
                // Preserve that exact measured source shading baseline; procedural assets retain their gate.
                if(doc.normalOrientationScore>0?Mathf.Abs(score-doc.normalOrientationScore)>.000001f:score<.985f)throw new Exception("Winding/normal inconsistency: "+doc.key+" "+score);
                foreach(var c in root.GetComponentsInChildren<Collider>()){c.contactOffset=.001f;if(c.isTrigger)throw new Exception("Trigger hitbox");}
                string prefab=output+"/"+doc.key+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);paths.Add(prefab);
                var entry=new Item{key=doc.key,size=bounds.size,triangles=triangles,colliders=root.GetComponentsInChildren<Collider>().Length,renderers=root.GetComponentsInChildren<Renderer>().Length,glass=doc.parts.Any(p=>p.glass),normalOrientationScore=score};items.Add(entry);
                Render(root,bounds,doc.key);if(doc.parts.Any(p=>p.glass))TransmissionProbe(root,bounds,doc);Settle(root,entry,doc);ContactChecks(root,bounds,entry);Clearance(root,entry,doc);AuditMaterials(root,entry,doc,folder);AuditGeometry(root,entry,doc);UnityEngine.Object.DestroyImmediate(root);index++;
            }
            if(items.Count!=20)throw new Exception("Expected twenty valuables");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            for(int i=0;i<paths.Count;i++){var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));o.transform.position=new Vector3(i*.7f,0,0);}
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Generated/ValuablesReview.unity");
            AssetDatabase.SaveAssets();Directory.CreateDirectory(Root+"/dist/bundles");
            var built=BuildPipeline.BuildAssetBundles(Root+"/dist/bundles",new[]{new AssetBundleBuild{assetBundleName="contour4-valuables.bundle",assetNames=paths.ToArray()}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
            if(!built)throw new Exception("Native bundle build did not return a manifest");
            var loaded=AssetBundle.LoadFromFile(Root+"/dist/bundles/contour4-valuables.bundle");if(!loaded)throw new Exception("Built bundle could not be reloaded");
            try{foreach(var path in paths){var prefab=loaded.LoadAsset<GameObject>(path);if(!prefab||prefab.GetComponentsInChildren<MeshRenderer>().Length<1)throw new Exception("Bundle roundtrip prefab missing "+path);}}finally{loaded.Unload(false);}
            File.WriteAllText(Root+"/evidence/unity-report.json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,success=true,items=items.ToArray()},true));
            Debug.Log("CONTOUR4_VALUABLES_BUILD_OK");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Texture2D Texture(string path,bool srgb,bool normal,int size){var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;importer.maxTextureSize=size;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;importer.wrapMode=TextureWrapMode.Clamp;importer.flipGreenChannel=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
    static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
    static Material SurfaceMaterial(Surface surface,bool glass,string folder,int size){
        var material=new Material(Shader.Find("Standard"));var c=surface.color;
        material.color=new Color(c[0],c[1],c[2],c[3]);material.SetFloat("_Metallic",surface.metallic);material.SetFloat("_Glossiness",surface.smoothness);
        if(!string.IsNullOrEmpty(surface.colorTexture))material.SetTexture("_MainTex",Texture(folder+"/"+surface.colorTexture,true,false,size));
        if(!string.IsNullOrEmpty(surface.normalTexture)){material.SetTexture("_BumpMap",Texture(folder+"/"+surface.normalTexture,false,true,size));material.EnableKeyword("_NORMALMAP");}
        if(!string.IsNullOrEmpty(surface.metalTexture)){material.SetTexture("_MetallicGlossMap",Texture(folder+"/"+surface.metalTexture,false,false,size));material.SetFloat("_GlossMapScale",1);material.EnableKeyword("_METALLICGLOSSMAP");}
        if(glass){material.SetFloat("_Mode",3);material.SetOverrideTag("RenderType","Transparent");material.SetInt("_SrcBlend",(int)BlendMode.One);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHAPREMULTIPLY_ON");material.renderQueue=3000;}
        return material;
    }
    static float Orientation(Document doc){
        int good=0,total=0;foreach(var p in doc.parts)for(int j=0;j<p.indices.Length;j+=3){int a=p.indices[j],b=p.indices[j+1],c=p.indices[j+2];
            Func<float[],int,Vector3> at=(v,i)=>new Vector3(v[i*3],v[i*3+1],v[i*3+2]);var cross=Vector3.Cross(at(p.vertices,b)-at(p.vertices,a),at(p.vertices,c)-at(p.vertices,a));
            if(cross.sqrMagnitude<1e-20f)continue;total++;if(Vector3.Dot(cross,at(p.normals,a)+at(p.normals,b)+at(p.normals,c))>0)good++;}
        return (float)good/Mathf.Max(1,total);
    }
    static Part[] MergedParts(Document doc){
        return doc.parts.GroupBy(p=>p.material!=null?p.material.key:(p.glass?"glass":"opaque")).Select(group=>{
            var first=group.First();var vertices=new List<float>();var normals=new List<float>();var uv=new List<float>();var indices=new List<int>();
            foreach(var p in group){if(p.glass!=first.glass)throw new Exception("Mixed transmission material "+group.Key);int offset=vertices.Count/3;vertices.AddRange(p.vertices);normals.AddRange(p.normals);uv.AddRange(p.uv);indices.AddRange(p.indices.Select(i=>i+offset));}
            return new Part{name=group.Key,glass=first.glass,material=first.material,vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),indices=indices.ToArray()};
        }).ToArray();
    }
    static void AuditGeometry(GameObject root,Item item,Document doc){
        if(OriginalKeys.Contains(doc.key))return;
        var parts=MergedParts(doc);var meshes=root.GetComponentsInChildren<MeshFilter>();if(meshes.Length!=parts.Length)throw new Exception("Material part count changed "+doc.key);
        foreach(var part in parts){var mesh=meshes.Single(m=>m.name==part.name).sharedMesh;
            if(!mesh.triangles.SequenceEqual(part.indices))throw new Exception("Topology changed "+part.name);var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
            if(vertices.Length*3!=part.vertices.Length||uv.Length*2!=part.uv.Length)throw new Exception("Vertex/UV count changed "+part.name);
            for(int j=0;j<vertices.Length;j++){
                item.packedPositionError=Mathf.Max(item.packedPositionError,Vector3.Distance(vertices[j],new Vector3(part.vertices[j*3],part.vertices[j*3+1],part.vertices[j*3+2])));
                item.packedNormalError=Mathf.Max(item.packedNormalError,Vector3.Distance(normals[j],new Vector3(part.normals[j*3],part.normals[j*3+1],part.normals[j*3+2])));
                if(Vector2.Distance(uv[j],new Vector2(part.uv[j*2],part.uv[j*2+1]))>.000001f)throw new Exception("UV transfer changed "+part.name);
            }
        }
        if(item.packedPositionError>.000001f||item.packedNormalError>.000001f)throw new Exception("Exact native transfer changed "+doc.key);
    }
    static void AuditMaterials(GameObject root,Item item,Document doc,string folder){
        if(OriginalKeys.Contains(doc.key))return;var rows=new List<string>();
        foreach(var p in MergedParts(doc)){var mat=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name==p.name).sharedMaterial;var s=p.material;
            if(mat.shader.name!="Standard"||p.glass!=(mat.renderQueue==3000))throw new Exception("Material shader/mode changed "+p.name);
            if(Vector4.Distance(mat.color,new Vector4(s.color[0],s.color[1],s.color[2],s.color[3]))>.00001f)throw new Exception("Material tint changed "+p.name);
            if(p.glass&&(!mat.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")||mat.GetInt("_ZWrite")!=0||mat.color.a>=1))throw new Exception("Glass transmission mode missing "+p.name);
            var files=new[]{s.colorTexture,s.normalTexture,s.metalTexture};var roles=new[]{"_MainTex","_BumpMap","_MetallicGlossMap"};
            for(int j=0;j<files.Length;j++){if(string.IsNullOrEmpty(files[j]))continue;var tex=mat.GetTexture(roles[j]);var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/"+files[j]);
                if(!tex||tex.width>doc.textureSize||tex.height>doc.textureSize||importer.sRGBTexture!=(j==0)||((importer.textureType==TextureImporterType.NormalMap)!=(j==1))||importer.flipGreenChannel||tex.isDataSRGB!=(j==0))throw new Exception("Material texture flags changed "+doc.key+" "+files[j]);
                if(j==1&&!mat.IsKeywordEnabled("_NORMALMAP")||j==2&&!mat.IsKeywordEnabled("_METALLICGLOSSMAP"))throw new Exception("Packed texture shader keyword missing "+p.name);
                rows.Add(p.name+":"+roles[j]+":"+tex.width+"x"+tex.height+":"+(tex.isDataSRGB?"sRGB":"linear"));
            }
        }
        item.materialShader="Standard";item.packedTextures=rows.ToArray();
    }
    static void Clearance(GameObject root,Item item,Document doc){
        var probes=new List<Probe>(doc.clearProbes??Array.Empty<Probe>());
        Action<string,float,float,float> add=(name,x,y,z)=>probes.Add(new Probe{name=name,position=new[]{x,y,z}});
        switch(doc.key){
            case "free-fall-photogate":add("Photogate open measuring channel",0,.022f,-.02f);break;
            case "anomaly-cryostat":foreach(float sign in new[]{-1f,1f})add("Carry rail interior",sign*.53f,.97f,0);break;
            case "kater-pendulum":add("Column carry aperture",-.22f,.465f,0);add("Upper arch aperture",0,.88f,0);break;
            case "vertical-seismometer":foreach(float sign in new[]{-1f,1f})add("Side base grip pocket",sign*.23f,.045f,0);break;
            case "vacuum-drop-column":add("Open tripod outside central housing",.45f,.45f,.10f);break;
            case "cavendish-balance":add("Right handle opening",.8494f,.306f,.0917f);add("Left handle opening",-.854f,.304f,.119f);break;
        }
        foreach(var p in probes)foreach(var c in root.GetComponentsInChildren<Collider>())if(Vector3.Distance(c.ClosestPoint(V(p.position)),V(p.position))<.001f)throw new Exception("Blocked opening "+doc.key+" "+p.name);
        item.clearProbes=(item.clearProbes??Array.Empty<string>()).Concat(probes.Select(p=>p.name)).ToArray();
    }
    static void Box(Transform parent,string name,Vector3 center,Vector3 size){var go=new GameObject(name);go.transform.SetParent(parent,false);var box=go.AddComponent<BoxCollider>();box.center=center;box.size=size;}
    static void LabColliders(Document doc,Transform parent){
        var candidates=doc.shapeCandidates;
        Action<string> use=name=>{var s=candidates.Single(x=>x.name==name);Box(parent,s.name,V(s.center),V(s.size));};
        Action<Func<Shape,bool>> select=predicate=>{foreach(var s in candidates.Where(predicate))use(s.name);};
        // Explicit anatomy: an overall bounds box would close the photogate, pendulum and handles.
        switch(doc.key){
            case "corner-cube":case "bullseye-level":use("geometry_0");break;
            case "free-fall-photogate":
                Box(parent,"Sensor bridge",new Vector3(0,.032f,.047f),new Vector3(.132f,.061f,.045f));
                foreach(float sign in new[]{-1f,1f})Box(parent,"Photogate arm",new Vector3(sign*.052f,.022f,-.023f),new Vector3(.027f,.041f,.094f));
                Box(parent,"Mounting lug",new Vector3(0,.061f,.045f),new Vector3(.025f,.026f,.025f));break;
            case "pirani-gauge-head":use("geometry_0");break;
            case "anomaly-cryostat":
                use("V2_Continuous_CreamBarrel");use("Top_Clean_Ivory_Pressure_Dome");use("V2_Base_Continuous_MetalCollar");use("Top_Sealing_Collar");use("Instrument_Housing");
                select(s=>s.name.Contains("_Shoe")||s.name.Contains("RailMountPlate")||s.name=="Regulator_Canister"||s.name.StartsWith("Pressure_")&&s.name.EndsWith("Brass_Bezel"));
                foreach(float sign in new[]{-1f,1f}){
                    Box(parent,"Outer carry rail",new Vector3(sign*.574f,1.08f,0),new Vector3(.036f,.036f,.48f));
                    foreach(float z in new[]{-.235f,.235f})Box(parent,"Carry rail stanchion",new Vector3(sign*.52f,.96f,z),new Vector3(.13f,.24f,.036f));
                }break;
            case "atwood-machine":
                select(s=>s.name=="Meshy_Base"||s.name=="Meshy_PulleyDisk"||s.name=="Meshy_PulleyAxleSupport"||s.name.Contains("HangingMass")||s.name=="Meshy_part_14"||s.name=="Meshy_part_13"||s.center[1]-s.size[1]/2<.005f);break;
            case "kater-pendulum":
                select(s=>s.name=="Meshy_Base"||s.name.Contains("ClampMass")||s.name.Contains("KnifeEdge")||s.name=="Meshy_FreePendulumBar"||s.center[1]-s.size[1]/2<.005f);
                Box(parent,"Column main",new Vector3(-.153f,.515f,.002f),new Vector3(.075f,.80f,.105f));
                Box(parent,"Grip outer border",new Vector3(-.25f,.465f,.002f),new Vector3(.019f,.17f,.105f));
                foreach(float y in new[]{.374f,.556f})Box(parent,"Grip end",new Vector3(-.215f,y,.002f),new Vector3(.075f,.025f,.105f));
                Box(parent,"Upper arch",new Vector3(-.004f,.934f,0),new Vector3(.26f,.03f,.095f));
                foreach(float x in new[]{-.12f,.115f})Box(parent,"Arch end",new Vector3(x,.882f,0),new Vector3(.028f,.085f,.095f));break;
            case "vertical-seismometer":
                select(s=>s.name.StartsWith("LocalRepair_ClearGuard_")||s.center[1]-s.size[1]/2<.005f);
                Box(parent,"Base solid core",new Vector3(0,.046f,0),new Vector3(.40f,.06f,.30f));
                foreach(float sign in new[]{-1f,1f}){
                    foreach(float y in new[]{.020f,.071f})Box(parent,"Side grip border",new Vector3(sign*.23f,y,0),new Vector3(.04f,.010f,.35f));
                    foreach(float z in new[]{-.164f,.164f})Box(parent,"Side grip end",new Vector3(sign*.23f,.045f,z),new Vector3(.04f,.06f,.023f));
                }break;
            case "hydrostatic-balance":
                select(s=>s.name=="Meshy_Base"||s.name=="Meshy_Column"||s.name=="Meshy_BalanceBeam"||s.name=="Meshy_BeakerSeat"||s.name.EndsWith("Pan")||s.name=="Meshy_ClearBeaker"||s.center[1]-s.size[1]/2<.005f);break;
            case "vacuum-drop-column":
                select(s=>new[]{"Meshy_Source_001","Meshy_Source_000","Meshy_Source_019","Meshy_Source_017","Meshy_Source_042"}.Contains(s.name)||s.center[1]-s.size[1]/2<.01f);
                // Three sloping legs, split into short boxes rather than one filled tripod volume.
                foreach(var name in new[]{"Meshy_Source_053","Meshy_Source_074","Meshy_Source_022"}){
                    var leg=candidates.Single(s=>s.name==name);var c=V(leg.center);var size=V(leg.size);
                    for(int j=0;j<6;j++){float t=(j+.5f)/6;var center=c;center.y=c.y+(t-.5f)*size.y;center.x=c.x+(c.x>0?-1:1)*(t-.5f)*size.x*.75f;center.z=c.z+(c.z>0?-1:1)*(t-.5f)*size.z*.75f;Box(parent,"Tripod leg segment",center,new Vector3(size.x*.3f,size.y/6,size.z*.3f));}
                }break;
            case "cavendish-balance":
                select(s=>s.name.StartsWith("Glass_")||s.name.StartsWith("Neck_glass_")||new[]{"Meshy_Source_010","Meshy_Source_011","Meshy_Source_013","Meshy_Source_008","Meshy_Source_009","Swivel_fixed_bearing_foot"}.Contains(s.name)||s.center[1]-s.size[1]/2<.005f);
                foreach(var name in new[]{"Meshy_Source_006","Meshy_Source_007"}){
                    var s=candidates.Single(x=>x.name==name);var c=V(s.center);var size=V(s.size);float t=.024f;
                    Box(parent,"Carry handle upper bar",c+Vector3.up*(size.y-t)*.5f,new Vector3(size.x,t,size.z));
                    foreach(float sign in new[]{-1f,1f})Box(parent,"Carry handle end",c+Vector3.forward*sign*(size.z-t)*.5f,new Vector3(size.x,size.y,t));
                }break;
            default:throw new Exception("Missing collider recipe "+doc.key);
        }
    }
    static void AddColliders(Document doc,Transform parent)
    {
        if(!OriginalKeys.Contains(doc.key)){LabColliders(doc,parent);return;}
        if(doc.key=="field-core"){
            // The broad bottom is a pallet, not a solid AABB across lifting-eye holes.
            Box(parent,"Cast base",new Vector3(0,.08f,.04f),new Vector3(1.69f,.16f,1.30f));
            Box(parent,"Central winding body",new Vector3(0,.44f,.02f),new Vector3(.87f,.52f,.91f));
            Box(parent,"Upper central cap",new Vector3(0,.78f,.02f),new Vector3(.58f,.19f,.60f));
            foreach(float sign in new[]{-1f,1f}){
                Box(parent,"Side coil housing",new Vector3(sign*.647f,.48f,.09f),new Vector3(.29f,.59f,.92f));
                Box(parent,"Front frame rib",new Vector3(sign*.47f,.49f,-.40f),new Vector3(.14f,.61f,.16f));
                Box(parent,"Rear frame rib",new Vector3(sign*.47f,.49f,.44f),new Vector3(.14f,.61f,.16f));
                // Low projecting lugs: clear center, three contact bars around each eye.
                float x=sign*.57f;
                Box(parent,"Lifting eye lower bar",new Vector3(x,.036f,-.697f),new Vector3(.15f,.034f,.144f));
                Box(parent,"Lifting eye upper bar",new Vector3(x,.121f,-.681f),new Vector3(.15f,.034f,.12f));
                foreach(float edge in new[]{-1f,1f})Box(parent,"Lifting eye side",new Vector3(x+edge*.061f,.078f,-.69f),new Vector3(.028f,.085f,.12f));
            }
            Box(parent,"Front control box",new Vector3(.43f,.34f,-.574f),new Vector3(.36f,.37f,.16f));
            return;
        }
        if(new[]{"portable-gravimeter","gimbal-gyroscope","gravity-lens","mobius-strip"}.Contains(doc.key)){NewColliders(doc,parent);return;}
        if(doc.key=="calibration-weight") {
            Box(parent,"Lower cast iron body",new Vector3(0,.0875f,0),new Vector3(.33f,.175f,.30f));
            Box(parent,"Top face",new Vector3(0,.2825f,0),new Vector3(.33f,.035f,.30f));
            Box(parent,"Recess back walls",new Vector3(0,.22f,0),new Vector3(.28f,.09f,.25f));
            foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})Box(parent,"Grip corner pillar",new Vector3(x*.1525f,.22f,z*.1375f),new Vector3(.025f,.09f,.025f));return;
        }
        foreach(var s in doc.shapes)
        {
            bool include=doc.key=="experiment-cassette"?(s.name=="lower casing"||s.name=="ivory lid"||s.name.StartsWith("latch")||s.name=="control knob"):
                doc.key=="control-sample"?(!s.name.StartsWith("specimen")&&s.name!="suspended rock"&&s.name!="lid dial"&&s.name!="carry handle"):
                (s.name=="main shaft"||s.name=="hand grip"||s.name.StartsWith("instrument body")||s.name.StartsWith("end cap")||s.name.StartsWith("body rim")||s.name.StartsWith("sensor cage")||s.name.StartsWith("sensor core")||s.name.StartsWith("sensor ring")||s.name.StartsWith("gauge bezel")||s.name.StartsWith("grip collar"));
            if(include)Box(parent,s.name,V(s.center),V(s.size));
        }
        if(doc.key=="control-sample"){
            var s=doc.shapes.First(x=>x.name=="carry handle");Vector3 c=V(s.center),size=V(s.size);float thickness=.006f;
            Box(parent,"Carry handle top",c+Vector3.up*(size.y*.5f-thickness*.5f),new Vector3(size.x,thickness,size.z));
            foreach(float sign in new[]{-1f,1f})Box(parent,"Carry handle side",c+Vector3.right*sign*(size.x*.5f-thickness*.5f),new Vector3(thickness,size.y,size.z));
        }
    }
    static void Settle(GameObject root,Item entry,Document doc)
    {
        var floor=new GameObject("Physics check floor");var collider=floor.AddComponent<BoxCollider>();collider.center=new Vector3(0,-.05f,0);collider.size=new Vector3(8,.1f,8);
        root.transform.position=Vector3.up*.06f;var rb=root.AddComponent<Rigidbody>();rb.mass=Mass(doc);entry.mass=rb.mass;rb.centerOfMass=new Vector3(0,Size(doc).y*(entry.key=="field-core"?.325f:.5f),0);rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=12;
        var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();for(int i=0;i<300;i++)Physics.Simulate(.02f);Physics.simulationMode=previous;
        Bounds b=root.GetComponentsInChildren<Collider>().First().bounds;foreach(var c in root.GetComponentsInChildren<Collider>())b.Encapsulate(c.bounds);
        entry.settledBottom=b.min.y;entry.settledSpeed=rb.linearVelocity.magnitude;entry.settled=!float.IsNaN(entry.settledBottom)&&entry.settledBottom>-.005f&&entry.settledBottom<.006f&&entry.settledSpeed<.02f;
        UnityEngine.Object.DestroyImmediate(rb);UnityEngine.Object.DestroyImmediate(floor);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
        if(!entry.settled)throw new Exception("Settle check failed: "+entry.key+" bottom="+entry.settledBottom+" speed="+entry.settledSpeed);
    }
    static void NewColliders(Document doc,Transform parent)
    {
        foreach(var s in doc.shapes){
            string n=s.name;Vector3 c=V(s.center),size=V(s.size);
            // Covered seams and internal bearing details do not need duplicate dynamic contacts.
            if(doc.key=="gravity-lens"&&n=="Lens retaining ring")continue;
            if(doc.key=="gimbal-gyroscope"&&(n.StartsWith("Inner hoop bearing")||n.StartsWith("Middle hoop bearing")||n.StartsWith("Outer hoop bearing")||n.StartsWith("Outer ring bearing")||n.StartsWith("Inner trunnion")||n.StartsWith("Middle trunnion")||n.StartsWith("Rotor bearing")||n=="Rotor hub"||n.StartsWith("Frame bearing")))continue;
            if(n=="Carry handle"||n.StartsWith("Loop handle")){
                float t=n=="Carry handle"?.018f:.018f;
                Box(parent,n+" top",c+Vector3.up*(size.y-t)*.5f,new Vector3(size.x,t,size.z));
                Box(parent,n+" bottom",c-Vector3.up*(size.y-t)*.5f,new Vector3(size.x,t,size.z));
                foreach(float sign in new[]{-1f,1f})Box(parent,n+" side",c+Vector3.right*sign*(size.x-t)*.5f,new Vector3(t,size.y,size.z));continue;
            }
            bool ring=n=="Inner hoop"||n=="Middle hoop"||n=="Outer hoop"||n=="Lens retaining ring"||n=="Lens steel rim";
            bool ribbon=n=="MOBIUS welded mid-surface";
            if(ring||ribbon){
                var p=doc.parts.First(x=>x.name.StartsWith(n+" / "));int sectors=ribbon?32:doc.key=="gimbal-gyroscope"?8:12;
                for(int sector=0;sector<sectors;sector++){
                    var vs=new List<Vector3>();
                    for(int j=0;j<p.indices.Length;j+=3){var points=new Vector3[3];for(int k=0;k<3;k++){int o=p.indices[j+k]*3;points[k]=new Vector3(p.vertices[o],p.vertices[o+1],p.vertices[o+2]);}var mid=(points[0]+points[1]+points[2])/3-c;
                        float angle=Mathf.Atan2(mid.y,mid.x)+Mathf.PI;int bin=Mathf.Min(sectors-1,(int)(angle/(2*Mathf.PI)*sectors));if(bin==sector)vs.AddRange(points);
                    }
                    if(vs.Count>=6)Hull(parent,n+" segment "+sector,vs.ToArray(),doc.key);
                }continue;
            }
            if(n=="Single continuous red boundary"||n.StartsWith("Scale tick")||n.StartsWith("Needle")||n.Contains("bolt")||n.Contains("screw")||n.StartsWith("Gauge")||n=="Face plate"||n.StartsWith("Side ventilation"))continue;
            if(n=="Optical disk"){
                // Narrow physical glazing, following the actual oblate profile rather than its square AABB.
                var p=doc.parts.First(x=>x.name.StartsWith(n+" / "));var points=new Vector3[p.vertices.Length/3];for(int j=0;j<points.Length;j++)points[j]=new Vector3(p.vertices[j*3],p.vertices[j*3+1],p.vertices[j*3+2]);Hull(parent,n,points,doc.key);continue;
            }
            Box(parent,n,c,size);
        }
    }
    static void ContactChecks(GameObject root,Bounds visual,Item entry){
        Physics.SyncTransforms();var colliders=root.GetComponentsInChildren<Collider>();var allowed=visual;allowed.Expand(.002f);
        foreach(var c in colliders)if(!allowed.Contains(c.bounds.min)||!allowed.Contains(c.bounds.max))throw new Exception("Collider exceeds visual catalog extent: "+entry.key+" "+c.name);
        Vector3[] probes;string[] names;
        switch(entry.key){
            case "portable-gravimeter":probes=new[]{new Vector3(.0018f,.30f,.0084f)};names=new[]{"carry handle opening"};break;
            case "gimbal-gyroscope":probes=new[]{new Vector3(0,.723f,-.16f),new Vector3(-.55f,.30f,0),new Vector3(.36f,.50f,0)};names=new[]{"top handle clearance","side grip clearance","open frame outside rings"};break;
            case "gravity-lens":probes=new[]{new Vector3(.63f,.499f,0),new Vector3(-.63f,.499f,0),new Vector3(.35f,.86f,0)};names=new[]{"right loop opening","left loop opening","outside circular glazing inside AABB"};break;
            case "mobius-strip":probes=new[]{new Vector3(0,.26f,0)};names=new[]{"Mobius open center"};break;
            case "field-core":probes=new[]{new Vector3(-.57f,.078f,-.70f),new Vector3(.57f,.078f,-.70f)};names=new[]{"left lifting eye clearance","right lifting eye clearance"};break;
            default:probes=Array.Empty<Vector3>();names=Array.Empty<string>();break;
        }
        for(int i=0;i<probes.Length;i++)foreach(var c in colliders)if(Vector3.Distance(c.ClosestPoint(probes[i]),probes[i])<.003f)throw new Exception("Blocked "+entry.key+" "+names[i]+" by "+c.name+" bounds="+c.bounds+" closest="+c.ClosestPoint(probes[i])+" probe="+probes[i]+" enabled="+c.enabled);
        if(entry.key=="gravity-lens"&&!colliders.Any(c=>c.name=="Optical disk"&&Vector3.Distance(c.ClosestPoint(new Vector3(0,.51957f,0)),new Vector3(0,.51957f,0))<.001f))throw new Exception("Missing lens center contact");
        if(entry.key=="field-core"){
            var origins=new[]{new Vector3(0,2,0),new Vector3(2,.5f,.1f),new Vector3(.43f,.34f,-2)};
            var directions=new[]{Vector3.down,Vector3.left,Vector3.forward};
            for(int i=0;i<origins.Length;i++)if(!colliders.Any(c=>c.Raycast(new Ray(origins[i],directions[i]),out var hit,3)))throw new Exception("Core has no reachable contact surface "+i);
            if(Mathf.Abs(colliders.Min(c=>c.bounds.min.y))>.001f)throw new Exception("Core physical support does not match bottom pivot");
        }
        entry.contactFit=true;entry.clearProbes=names;
    }
    static void Hull(Transform parent,string name,Vector3[] vertices,string key){
        var mesh=new Mesh{name=name+" contact hull"};mesh.vertices=vertices;mesh.triangles=Enumerable.Range(0,vertices.Length).ToArray();mesh.RecalculateBounds();
        string path="Assets/Generated/"+key+"/Collider-"+name.Replace(' ','-')+".asset";AssetDatabase.CreateAsset(mesh,path);
        Physics.BakeMesh(mesh.GetInstanceID(),true);
        var go=new GameObject(name);go.transform.SetParent(parent,false);var collider=go.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=mesh;
    }
    static void Transmission(GameObject model,Bounds bounds){
        var glass=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial.renderQueue==3000);
        var target=new GameObject("Transmission contrast target");
        for(int x=0;x<4;x++)for(int y=0;y<4;y++){var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.transform.SetParent(target.transform,false);tile.transform.position=new Vector3((x-1.5f)*.12f,.51957f+(y-1.5f)*.12f,.16f);tile.transform.localScale=new Vector3(.12f,.12f,.02f);var mat=new Material(Shader.Find("Unlit/Color"));mat.color=(x+y)%2==0?new Color(1,.15f,.03f):new Color(.03f,.7f,1);tile.GetComponent<Renderer>().sharedMaterial=mat;}
        var go=new GameObject("Transmission camera");var camera=go.AddComponent<Camera>();camera.transform.position=new Vector3(0,.51957f,-2);camera.transform.LookAt(new Vector3(0,.51957f,0));camera.orthographic=true;camera.orthographicSize=.58f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
        var rt=new RenderTexture(600,600,24);camera.targetTexture=rt;
        Func<Color[]> capture=()=>{camera.Render();RenderTexture.active=rt;var tex=new Texture2D(600,600,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,600,600),0,0);tex.Apply();var pixels=tex.GetPixels();if(glass.enabled)File.WriteAllBytes(Root+"/evidence/gravity-lens-transmission-unity.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);return pixels;};
        var through=capture();glass.enabled=false;var clear=capture();target.SetActive(false);var background=capture();glass.enabled=true;
        float diffClear=0,diffBackground=0;int count=0;for(int y=190;y<410;y++)for(int x=190;x<410;x++){int p=y*600+x;diffClear+=(new Vector3(through[p].r-clear[p].r,through[p].g-clear[p].g,through[p].b-clear[p].b)).magnitude;diffBackground+=(new Vector3(through[p].r-background[p].r,through[p].g-background[p].g,through[p].b-background[p].b)).magnitude;count++;}
        diffClear/=count;diffBackground/=count;File.WriteAllText(Root+"/evidence/lens-transmission.json","{\"targetPixels\":"+count+",\"meanDifferenceFromClearTarget\":"+diffClear.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"meanDifferenceFromNoTarget\":"+diffBackground.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"pass\":"+(diffBackground>diffClear*2?"true":"false")+"}");
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(target);if(!(diffBackground>diffClear*2))throw new Exception("Lens target did not transmit");
    }
    static void TransmissionProbe(GameObject model,Bounds bounds,Document doc){
        var renderers=model.GetComponentsInChildren<MeshRenderer>();Part selected=null;Vector3 a=Vector3.zero,b=Vector3.zero,c=Vector3.zero;float best=0;
        foreach(var part in doc.parts.Where(p=>p.glass))for(int i=0;i<part.indices.Length;i+=3){
            Func<int,Vector3> vertex=j=>new Vector3(part.vertices[j*3],part.vertices[j*3+1],part.vertices[j*3+2]);var va=vertex(part.indices[i]);var vb=vertex(part.indices[i+1]);var vc=vertex(part.indices[i+2]);float area=Vector3.Cross(vb-va,vc-va).magnitude;
            if(area>best){best=area;selected=part;a=va;b=vb;c=vc;}
        }
        if(selected==null)throw new Exception("No glass surface triangle "+doc.key);
        var pane=renderers.Single(r=>r.name==selected.material.key);var center=(a+b+c)/3;
        var forward=-Vector3.Cross(b-a,c-a).normalized;var up=(b-a).normalized;var right=Vector3.Cross(up,forward);float extent=best/(Vector3.Distance(a,b)+Vector3.Distance(b,c)+Vector3.Distance(c,a))*.8f;
        // Isolate the real authored glass to distinguish transmission from an opaque inner apparatus.
        foreach(var renderer in renderers)renderer.enabled=renderer==pane;
        var target=new GameObject("Glass transmission checker");
        for(int x=0;x<4;x++)for(int y=0;y<4;y++){var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.transform.SetParent(target.transform,false);tile.transform.position=center+forward*.006f+right*((x-1.5f)*extent*.45f)+up*((y-1.5f)*extent*.45f);tile.transform.rotation=Quaternion.LookRotation(forward,up);tile.transform.localScale=new Vector3(extent*.45f,extent*.45f,.001f);var material=new Material(Shader.Find("Unlit/Color"));material.color=(x+y)%2==0?new Color(1,.15f,.03f):new Color(.03f,.7f,1);tile.GetComponent<Renderer>().sharedMaterial=material;}
        var go=new GameObject("Transmission camera");var camera=go.AddComponent<Camera>();camera.transform.position=center-forward*2;camera.transform.rotation=Quaternion.LookRotation(forward,up);camera.orthographic=true;camera.orthographicSize=extent;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
        var rt=new RenderTexture(320,320,24);camera.targetTexture=rt;
        Func<Color[]> capture=()=>{camera.Render();RenderTexture.active=rt;var tex=new Texture2D(320,320,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,320,320),0,0);tex.Apply();var pixels=tex.GetPixels();if(pane.enabled)File.WriteAllBytes(Root+"/evidence/"+doc.key+"-transmission-unity.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);return pixels;};
        var through=capture();pane.enabled=false;var clear=capture();target.SetActive(false);var background=capture();float diffClear=0,diffBackground=0;int count=0;
        for(int y=120;y<200;y++)for(int x=120;x<200;x++){int p=y*320+x;diffClear+=Vector3.Distance(new Vector3(through[p].r,through[p].g,through[p].b),new Vector3(clear[p].r,clear[p].g,clear[p].b));diffBackground+=Vector3.Distance(new Vector3(through[p].r,through[p].g,through[p].b),new Vector3(background[p].r,background[p].g,background[p].b));count++;}
        Func<Color[],float> contrast=pixels=>Vector3.Distance(new Vector3(pixels[140*320+140].r,pixels[140*320+140].g,pixels[140*320+140].b),new Vector3(pixels[140*320+180].r,pixels[140*320+180].g,pixels[140*320+180].b));
        float contrastRetention=contrast(through)/Mathf.Max(.0001f,contrast(clear));
        // A .24-alpha closed glass shell has two surfaces: retain >35% checker modulation.
        // Compare spatial target contrast, not tint distance from the unglazed target.
        var culture=System.Globalization.CultureInfo.InvariantCulture;bool pass=contrastRetention>.35f&&diffBackground>count*.05f;
        File.WriteAllText(Root+"/evidence/"+doc.key+"-transmission.json","{\"isolatedAuthoredPane\":true,\"pixels\":"+count+",\"contrastRetention\":"+contrastRetention.ToString(culture)+",\"differenceClear\":"+(diffClear/count).ToString(culture)+",\"differenceNoTarget\":"+(diffBackground/count).ToString(culture)+",\"pass\":"+(pass?"true":"false")+"}");
        foreach(var renderer in renderers)renderer.enabled=true;RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(target);if(!pass)throw new Exception("Authored glass does not transmit "+doc.key);
    }
    static void Render(GameObject model,Bounds bounds,string key)
    {
        RenderSettings.ambientLight=new Color(.42f,.42f,.42f);RenderSettings.ambientMode=AmbientMode.Flat;
        var light=new GameObject("Review light");light.transform.rotation=Quaternion.Euler(35,-35,0);var l=light.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.7f;
        var camGo=new GameObject("Review camera");var camera=camGo.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*.78f;camera.nearClipPlane=.001f;camera.farClipPlane=100;camera.transform.position=bounds.center+new Vector3(2,1.6f,-3).normalized*3;camera.transform.LookAt(bounds.center);
        var rt=new RenderTexture(800,800,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var png=new Texture2D(800,800,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,800,800),0,0);png.Apply();File.WriteAllBytes(Root+"/evidence/"+key+"-unity.png",png.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(png);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camGo);UnityEngine.Object.DestroyImmediate(light);
    }
}


