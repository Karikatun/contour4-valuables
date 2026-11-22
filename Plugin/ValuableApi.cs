using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;
using UnityEngine.AI;

namespace Contour4Valuables;

/// <summary>Stable map integration API. Positions are bottom-center pivots in room orientation.</summary>
public static class ValuableApi
{
    internal static readonly Dictionary<string,GameObject> Prefabs = new(StringComparer.Ordinal);
    internal static readonly Dictionary<string,PrefabRef> References = new(StringComparer.Ordinal);
    public static bool IsReady { get; internal set; }
    public static string[] Keys => new[]{"experiment-cassette","control-sample","sensor-rod","calibration-weight","portable-gravimeter","gimbal-gyroscope","gravity-lens","mobius-strip","field-core"};
    public static GameObject GetPrefab(string key)
    {
        if(!IsReady)throw new InvalidOperationException("Contour4 valuables registration is incomplete.");
        return Prefabs.TryGetValue(key,out var prefab)?prefab:throw new ArgumentException("Unknown Contour4 valuable key: "+key,nameof(key));
    }
    public static GameObject Spawn(string key,Vector3 position,Quaternion rotation)
    {
        GetPrefab(key);
        if(!SemiFunc.IsMasterClientOrSingleplayer())throw new InvalidOperationException("Only the raid host may spawn valuables.");
        var result=Valuables.SpawnValuable(References[key],position,rotation);
        if(!result)throw new InvalidOperationException("REPOLib failed to spawn "+key);
        Contour4ValuablesPlugin.SetSpawnPrice(result.GetComponent<ValuableObject>());
        return result;
    }
}

[BepInPlugin(Id,"Contour4 Valuables","0.2.3")]
[BepInDependency("REPOLib",BepInDependency.DependencyFlags.HardDependency)]
public sealed class Contour4ValuablesPlugin:BaseUnityPlugin
{
    public const string Id="spacejourney.contour4.valuables";
    GameObject? staging;
    AssetBundle? bundle;
    static readonly AccessTools.FieldRef<ValuableObject,int> Override=AccessTools.FieldRefAccess<ValuableObject,int>("dollarValueOverride");
    static readonly AccessTools.FieldRef<ValuableObject,bool> ValueReady=AccessTools.FieldRefAccess<ValuableObject,bool>("dollarValueSet");
    static readonly AccessTools.FieldRef<ValuableObject,float> Current=AccessTools.FieldRefAccess<ValuableObject,float>("dollarValueCurrent"), Original=AccessTools.FieldRefAccess<ValuableObject,float>("dollarValueOriginal");
    internal static void SetSpawnPrice(ValuableObject value)
    {
        // Native random preset prices round to $100, which can put a $650 endpoint at $600.
        // The host rolls a catalog dollar amount before Start; the native coroutine initializes
        // and sends its normal buffered DollarValueSetRPC. Clients never roll independently.
        Override(value)=UnityEngine.Random.Range(Mathf.CeilToInt(value.valuePreset.valueMin),Mathf.FloorToInt(value.valuePreset.valueMax)+1);
    }
    static readonly Spec[] Specs={
        new("experiment-cassette","01-experiment-cassette",new(.24f,.08f,.16f),.8f,350,650,ValuableVolume.Type.Tiny,25,90),
        new("control-sample","02-control-sample",new(.24f,.60f,.24f),.7f,1200,2200,ValuableVolume.Type.Medium,90,20),
        new("sensor-rod","03-sensor-rod",new(.22f,1.65f,.22f),2.4f,1000,1800,ValuableVolume.Type.Tall,50,65),
        new("calibration-weight","04-calibration-weight",new(.33f,.30f,.30f),9,650,1100,ValuableVolume.Type.Small,25,90),
        new("portable-gravimeter","05-portable-gravimeter",new(.46f,.34f,.32f),3.2f,950,1600,ValuableVolume.Type.Small,50,65),
        new("gimbal-gyroscope","06-gimbal-gyroscope",new(1.18f,.76f,.55f),8,2300,3800,ValuableVolume.Type.Medium,50,65),
        new("gravity-lens","07-gravity-lens",new(1.35f,.95f,.52f),14,4200,6400,ValuableVolume.Type.Medium,70,40),
        new("mobius-strip","08-mobius-strip",new(.52f,.43f,.22f),2.1f,1800,3000,ValuableVolume.Type.Small,50,65),
        // Uniform catalog-envelope fit; Wide metadata is not enrollment in vanilla pools.
        // Actual depth exceeds native Wide. Explicit map placement must validate exact bounds.
        new("field-core","11-field-core",new(1.7059345f,.9693324f,1.55f),36,48000,65000,ValuableVolume.Type.Wide,25,90)
    };
    void Awake()
    {
        try
        {
            var template=Resources.Load<GameObject>("valuables/03 medium/valuable manor trophy");
            if(!template||!template.GetComponent<ValuableObject>())throw new InvalidDataException("Native Trophy template unavailable.");
            staging=new GameObject("Contour4 valuable templates");staging.SetActive(false);DontDestroyOnLoad(staging);
            string assets=Path.Combine(Path.GetDirectoryName(Info.Location)!,"assets");
            bundle=AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location)!,"contour4-valuables.bundle"));
            if(!bundle)throw new InvalidDataException("Contour4 native Unity asset bundle is unavailable.");
            foreach(var spec in Specs)ValuableApi.Prefabs.Add(spec.Key,Create(template,spec,assets));
            foreach(var spec in Specs)
            {
                // Register network pickup without enrolling it into any random vanilla loot pool.
                // Valuables.RegisterValuable with an empty preset list silently enrolls Generic.
                var reference=NetworkPrefabs.RegisterNetworkPrefab("Valuables/Contour4 "+spec.Key,ValuableApi.Prefabs[spec.Key]);
                if(reference==null||!reference.IsValid())throw new InvalidOperationException("REPOLib registration failed: "+spec.Key);
                ValuableApi.References.Add(spec.Key,reference);
                Logger.LogInfo($"Contour4 registered {spec.Key}: {spec.Size.x:F3}x{spec.Size.y:F3}x{spec.Size.z:F3}m, mass={spec.Mass:F2}, value={spec.Min:F0}-{spec.Max:F0}, fragility={spec.Fragility:F2}, durability={spec.Durability:F0}, id={reference.ResourcePath}");
            }
            ValuableApi.IsReady=true;
            Logger.LogInfo("Contour4 valuables ready: nine registered network prefabs; explicit map spawns only; no vanilla Generic entries.");
        }
        catch(Exception e){ValuableApi.IsReady=false;Logger.LogError("Contour4 valuables initialization failed: "+e);}
    }
    GameObject Create(GameObject template,Spec spec,string assets)
    {
        var prefab=Instantiate(template,staging!.transform,false);prefab.name="Contour4 "+spec.Key;
        prefab.transform.localPosition=Vector3.zero;prefab.transform.localRotation=Quaternion.identity;prefab.transform.localScale=Vector3.one;
        var old=prefab.GetComponentsInChildren<Collider>(true).Where(c=>c.CompareTag("Phys Grab Object")).ToArray();if(old.Length==0)throw new InvalidDataException("Native grab colliders missing");int layer=old[0].gameObject.layer;
        foreach(var c in old)DestroyImmediate(c);foreach(var c in prefab.GetComponentsInChildren<PhysGrabObjectCollider>(true))DestroyImmediate(c);
        foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))if(renderer is MeshRenderer||renderer is SkinnedMeshRenderer)renderer.enabled=false;
        var obstacle=prefab.GetComponent<NavMeshObstacle>();if(obstacle)DestroyImmediate(obstacle);
        var source=bundle!.LoadAsset<GameObject>("assets/generated/"+spec.Key+"/"+spec.Key+".prefab");if(!source)throw new InvalidDataException("Missing authored prefab "+spec.Key);
        var content=Instantiate(source,prefab.transform,false);content.name="Contour4 Content";
        // Owner-approved 2x capsule: scale the complete authored hierarchy together,
        // keeping transparent glass, handle, compound colliders and bottom pivot aligned.
        if(spec.Key=="control-sample")content.transform.localScale*=2f;
        var renderers=content.GetComponentsInChildren<MeshRenderer>(true);if(renderers.Length==0)throw new InvalidDataException("Missing authored renderers "+spec.Key);
        Bounds bounds=MeshBoundsInRoot(renderers[0],prefab.transform);
        foreach(var renderer in renderers)bounds.Encapsulate(MeshBoundsInRoot(renderer,prefab.transform));
        if(Vector3.Distance(bounds.size,spec.Size)>.0001f||Mathf.Abs(bounds.min.y)>.0001f)throw new InvalidDataException("Catalog dimensions/pivot mismatch "+spec.Key);
        foreach(var transform in content.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=layer;
        foreach(var collider in content.GetComponentsInChildren<Collider>(true))
        {
            collider.gameObject.tag="Phys Grab Object";
            // Native RayCheck selects this Collider's GameObject, then dereferences its
            // PhysGrabObjectCollider. Native Start supplies parent linkage and collider IDs.
            // Install the bridge while staging is inactive, before either Start can run.
            if(collider.enabled&&!collider.isTrigger&&!collider.GetComponent<PhysGrabObjectCollider>())
                collider.gameObject.AddComponent<PhysGrabObjectCollider>();
        }
        var value=prefab.GetComponent<ValuableObject>();var price=ScriptableObject.CreateInstance<Value>();price.name=spec.Key+" catalog range";price.valueMin=spec.Min;price.valueMax=spec.Max;value.valuePreset=price;
        Override(value)=0;ValueReady(value)=false;Current(value)=0;Original(value)=0;
        var physical=ScriptableObject.CreateInstance<PhysAttribute>();physical.name=spec.Key+" catalog mass";physical.mass=spec.Mass;value.physAttributePreset=physical;
        var durability=ScriptableObject.CreateInstance<Durability>();durability.name=spec.Key+" prototype durability";durability.fragility=spec.Fragility;durability.durability=spec.Durability;value.durabilityPreset=durability;value.volumeType=spec.Volume;
        Vector3 center=bounds.center;if(spec.Key=="field-core")center.y=bounds.size.y*.325f;
        var rb=prefab.GetComponent<Rigidbody>();rb.mass=spec.Mass;rb.centerOfMass=center;
        prefab.GetComponent<PhysGrabObject>().colliders.Clear();
        var area=prefab.GetComponent<RoomVolumeCheck>();area.CheckPosition=bounds.center;area.currentSize=bounds.size+Vector3.one*.004f;
        Anchor(prefab.transform,"Center of Mass",center);Anchor(prefab.transform,"ForceCenterPoint",center);
        // Template children stay inactive until cloned outside the inactive staging parent.
        prefab.SetActive(true);return prefab;
    }
    static Bounds MeshBoundsInRoot(MeshRenderer renderer,Transform root)
    {
        // Mesh.bounds is mesh-local; staged inactive Collider.bounds is not reliable.
        // Transform all eight corners so scaling/rotations/offsets reach root-space checks.
        var local=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
        var matrix=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
        var result=new Bounds(matrix.MultiplyPoint3x4(local.min),Vector3.zero);
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?local.min.x:local.max.x,y==0?local.min.y:local.max.y,z==0?local.min.z:local.max.z)));
        return result;
    }
    static void Anchor(Transform root,string name,Vector3 location){var child=root.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(root,false);}child.localPosition=location;}
    readonly struct Spec { internal readonly string Key,Folder; internal readonly Vector3 Size; internal readonly float Mass,Min,Max,Fragility,Durability; internal readonly ValuableVolume.Type Volume; internal Spec(string key,string folder,Vector3 size,float mass,float min,float max,ValuableVolume.Type volume,float fragility,float durability){Key=key;Folder=folder;Size=size;Mass=mass;Min=min;Max=max;Volume=volume;Fragility=fragility;Durability=durability;} }
}
