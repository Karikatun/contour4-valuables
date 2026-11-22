"""Adopt the approved Meshy core with a uniform catalog-envelope fit; export no other item."""
import bpy, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix

root=Path(__file__).resolve().parents[1]
src=root.parent/'prototype/valuable-models/meshy-field-core-15000/field-core-15000.blend'
assert hashlib.sha256(src.read_bytes()).hexdigest()=='e38640f43e1d38746dd0f68bb9ecdf0b4f37c8354a1a8b431081908a80162b21'
native=root/'Native/11-field-core.blend'
folder=root/'Unity/Assets/Source/11-field-core';folder.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(src))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
original=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(p[a] for p in original) for a in range(3)))
hi=Vector(tuple(max(p[a] for p in original) for a in range(3)))
factor=min(2.35/(hi.x-lo.x),1.55/(hi.y-lo.y),1.35/(hi.z-lo.z))
origin=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
parts=[]
native_root=bpy.data.objects.new('field-core bottom-center root',None)
bpy.context.scene.collection.objects.link(native_root)
fit=Matrix.Translation(-origin*factor)@Matrix.Scale(factor,4)
for ob in objects:
    matrix=ob.matrix_world.copy();normal_matrix=matrix.to_3x3().inverted().transposed()
    # Preserve original mesh bytes/smooth normals; a parent at zero defines the ground pivot.
    me=ob.data;me.calc_loop_triangles()
    normals=[(normal_matrix@n.vector).normalized() for n in me.corner_normals]
    points=[(matrix@v.co-origin)*factor for v in me.vertices]
    ob.parent=native_root;ob.matrix_world=fit@matrix
    uv=me.uv_layers.active.data;vs=[];ns=[];uvs=[];indices=[]
    for tri in me.loop_triangles:
        for li in [tri.loops[0],tri.loops[2],tri.loops[1]]:
            p=points[me.loops[li].vertex_index];n=normals[li]
            vs.extend((p.x,p.z,p.y));ns.extend((n.x,n.z,n.y));uvs.extend(uv[li].uv);indices.append(len(indices))
    parts.append(dict(name=ob.name,glass=False,vertices=vs,normals=ns,uv=uvs,indices=indices))
images={}
for im in bpy.data.images:
    if im.packed_file:
        key=next((k for k in ['basecolor','roughness','metallic','normal'] if k in im.name.lower()),None)
        if key:images[key]=im
assert set(images)=={'basecolor','roughness','metallic','normal'},list(images)
for key,im in images.items():
    (folder/(dict(basecolor='color',roughness='roughness',metallic='metallic',normal='normal')[key]+'.png')).write_bytes(im.packed_file.data)
    assert tuple(im.size)==(1024,1024)
# Unity Standard: metallic in R and smoothness (1 - roughness) in A, both linear.
for key in ['roughness','metallic']:images[key].colorspace_settings.name='Non-Color'
m=list(images['metallic'].pixels);r=list(images['roughness'].pixels)
packed=bpy.data.images.new('Unity linear metallic smoothness',width=1024,height=1024,alpha=True)
packed.colorspace_settings.name='Non-Color';pixels=[0.]*(1024*1024*4)
for i in range(0,len(pixels),4):pixels[i]=m[i];pixels[i+3]=1-r[i]
packed.pixels.foreach_set(pixels);packed.filepath_raw=str(folder/'metal.png');packed.file_format='PNG';packed.save()
allp=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
size=[max(p[a] for p in allp)-min(p[a] for p in allp) for a in range(3)]
doc=dict(key='field-core',parts=parts,shapes=[],textureSize=1024,size=[size[0],size[2],size[1]],mass=36)
(folder/'mesh.json').write_text(json.dumps(doc,separators=(',',':')))
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.ops.wm.save_as_mainfile(filepath=str(native))
bpy.ops.object.select_all(action='DESELECT')
for ob in objects:ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(folder/'11-field-core.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP')
report=dict(source=str(src),sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),nativeSha256=hashlib.sha256(native.read_bytes()).hexdigest(),uniformScale=factor,sourceSize=list(hi-lo),nativeSizeXYZ=size,unitySizeXYZ=doc['size'],triangles=sum(len(p['indices'])//3 for p in parts),pivot='bottom-center',textureSize=1024,maps={k:hashlib.sha256(i.packed_file.data).hexdigest() for k,i in images.items()})
(root/'evidence/field-core-export.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
