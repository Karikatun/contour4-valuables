"""Exact approved native transfer: cryostat V5 and gravity laboratory #22–31.

No shape/UV rewrite, rescale, remesh, bake or source save. mesh.json is the
authoritative loop-normal transfer; FBX is an additional conventional handoff.
Harness2 Propose-Probe-Compose methodology only, no custom task adapter.
"""
import bpy, json, hashlib, shutil, math, re, sys
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
LAB = ROOT.parent / 'prototype/valuable-models/gravity-lab-meshy/textured'
EXPECTED = [1846,1826,1828,1933,3185,3225,3239,3191,5637,4040]
NAMES = ['22-corner-cube','23-free-fall-photogate','24-pirani-gauge-head',
         '25-bullseye-level','26-atwood-machine','27-kater-pendulum',
         '28-vertical-seismometer','29-hydrostatic-balance',
         '30-vacuum-drop-column','31-cavendish-balance']

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def safe(name): return re.sub('[^a-z0-9-]+','-',name.lower()).strip('-')
def u(v): return [float(v.x),float(v.z),float(v.y)]
def mesh_objects():
    # These are review-stage geometry, explicitly absent from all source receipts.
    return sorted([o for o in bpy.context.scene.objects if o.type=='MESH'
                   and not o.name.startswith('_Review_')],key=lambda o:o.name)

def signature(obs):
    h=hashlib.sha256()
    for o in obs:
        h.update(o.name.encode()); h.update(np.array(o.matrix_world,dtype='<f4').tobytes())
        h.update(np.array([tuple(v.co) for v in o.data.vertices],dtype='<f4').tobytes())
        h.update(np.array([v for p in o.data.polygons for v in p.vertices],dtype='<i4').tobytes())
        for uv in o.data.uv_layers:
            h.update(uv.name.encode());h.update(np.array([tuple(x.uv) for x in uv.data],dtype='<f4').tobytes())
    return h.hexdigest()

def bounds(points):
    lo=Vector([min(v[a] for v in points) for a in range(3)])
    hi=Vector([max(v[a] for v in points) for a in range(3)])
    return lo,hi

def image_input(socket):
    if not socket.is_linked:return None
    node=socket.links[0].from_node
    if node.type=='NORMAL_MAP':node=node.inputs['Color'].links[0].from_node
    assert node.type=='TEX_IMAGE',('Unsupported live material expression',node.type)
    assert node.image and node.image.packed_file,node.name
    return node

def save_original(image, filename, folder, maps):
    path=folder/filename
    raw=bytes(image.packed_file.data)
    if path.exists():assert path.read_bytes()==raw,path
    else:path.write_bytes(raw)
    maps[filename]={'sha256':sha(path),'size':list(image.size),
                    'sourceImage':image.name,'originalBytes':True,
                    'colorspace':image.colorspace_settings.name}
    return filename

def pack_mask(metal,rough,folder,filename,maps):
    assert tuple(metal.size)==tuple(rough.size)
    # Native pixels are read as linear data; no sRGB conversion of mask channels.
    assert metal.colorspace_settings.name=='Non-Color'
    assert rough.colorspace_settings.name=='Non-Color'
    m=np.empty(len(metal.pixels),np.float32);metal.pixels.foreach_get(m)
    r=np.empty(len(rough.pixels),np.float32);rough.pixels.foreach_get(r)
    assert np.isfinite(m).all() and np.isfinite(r).all()
    p=np.zeros_like(m).reshape((-1,4));p[:,0]=m[::4];p[:,3]=1-r[::4]
    im=bpy.data.images.new('UnityMask '+filename,width=metal.size[0],height=metal.size[1],alpha=True)
    im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(p.ravel())
    im.file_format='PNG';im.filepath_raw=str(folder/filename);im.save()
    reloaded=bpy.data.images.load(str(folder/filename),check_existing=False)
    reloaded.colorspace_settings.name='Non-Color'
    q=np.empty(len(reloaded.pixels),np.float32);reloaded.pixels.foreach_get(q)
    error=float(np.abs(p.ravel()-q).max());assert error<=1/255+1e-6,error
    maps[filename]={'sha256':sha(folder/filename),'size':list(im.size),
                    'colorspace':'Non-Color','channels':'R=metallic,A=1-roughness',
                    'maxQuantizationError':error,'originalBytes':False}
    bpy.data.images.remove(reloaded);bpy.data.images.remove(im)

def material_record(mat,folder,maps,cache,lab):
    if mat.name in cache:return cache[mat.name]
    bs=[n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED']
    assert len(bs)==1,mat.name
    b=bs[0];inputs=b.inputs
    transmission=float(inputs['Transmission Weight'].default_value)
    alpha=float(inputs['Alpha'].default_value)
    glass=transmission>.1 or alpha<.999
    color=list(inputs['Base Color'].default_value);color[3]=alpha
    tex={k:image_input(inputs[s]) for k,s in [('color','Base Color'),('metallic','Metallic'),('roughness','Roughness'),('normal','Normal')]}
    colorTex='';metalTex='';normalTex='';uvnames=set()
    for k,node in tex.items():
        if not node:continue
        if node.inputs['Vector'].is_linked:
            uv=node.inputs['Vector'].links[0].from_node
            assert uv.type=='UVMAP',(mat.name,uv.type)
            uvnames.add(uv.uv_map)
    assert len(uvnames)<=1,(mat.name,uvnames)
    uvname=next(iter(uvnames),'')
    prefix=safe(mat.name)
    if lab and not glass:
        colorTex='color.png';normalTex='normal.png';metalTex='metal.png'
        assert all(tex.values()),mat.name
    else:
        for channel,node in tex.items():
            if node:
                filename=prefix+'-'+channel+'.png'
                save_original(node.image,filename,folder,maps)
                if channel=='color':colorTex=filename
                if channel=='normal':normalTex=filename
        if tex['metallic'] or tex['roughness']:
            assert tex['metallic'] and tex['roughness'],mat.name
            metalTex=prefix+'-metal.png'
            pack_mask(tex['metallic'].image,tex['roughness'].image,folder,metalTex,maps)
    if colorTex:color=[1.,1.,1.,1.]
    if glass and transmission>.9 and alpha>.99:color[3]=.16
    rec={'key':mat.name,'colorTexture':colorTex,'normalTexture':normalTex,
         'metalTexture':metalTex,'color':color,
         'metallic':1. if metalTex else float(inputs['Metallic'].default_value),
         'smoothness':1. if metalTex else 1-float(inputs['Roughness'].default_value),
         'sourceAlpha':alpha,'transmission':transmission,'ior':float(inputs['IOR'].default_value),
         'unityGlassApproximation':glass and transmission>.9 and alpha>.99}
    cache[mat.name]=(rec,glass,uvname)
    return cache[mat.name]

reports=[]
for name in ['10-anomaly-cryostat']+NAMES:
    lab=name in NAMES
    src=LAB/name/(name+'.blend') if lab else ROOT/'Native'/(name+'.blend')
    sourceSha=sha(src)
    if lab:
        sourceReport=json.loads((src.parent/'texture-report.json').read_text())
        assert sourceSha==sourceReport['final_blend_sha256']
    else:assert sourceSha=='d63cdac417adfee47a4bac012ded5fca8761a6e4ede1a0fd4d4b8c52ab60e933'
    folder=ROOT/'Unity/Assets/Source'/name;folder.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(src))
    obs=mesh_objects();sig=signature(obs);points=[o.matrix_world@v.co for o in obs for v in o.data.vertices]
    lo,hi=bounds(points);size=u(hi-lo)
    assert abs(lo.z)<1e-6,(name,list(lo))
    maps={};materials={};parts=[];shapes=[];same=total=0;opposingArea=totalArea=0.
    if lab:
        for key in ['color','normal','roughness','metallic','orm']:
            shutil.copy2(src.parent/(key+'.png'),folder/(key+'.png'))
            im=bpy.data.images.load(str(folder/(key+'.png')),check_existing=False)
            assert tuple(im.size)==((1024,1024) if int(name[:2])>=30 else (512,512))
            maps[key+'.png']={'sha256':sha(folder/(key+'.png')),'size':list(im.size),'originalBytes':True}
        ims=[]
        for key in ['metallic','roughness']:
            im=bpy.data.images.load(str(folder/(key+'.png')),check_existing=False)
            im.colorspace_settings.name='Non-Color';ims.append(im)
        pack_mask(*ims,folder,'metal.png',maps)
    for o in obs:
        me=o.data;me.calc_loop_triangles();world=o.matrix_world;normalMatrix=world.to_3x3().inverted().transposed()
        pts=[world@v.co for v in me.vertices];ol,oh=bounds(pts)
        shapes.append({'name':o.name,'center':u((ol+oh)*.5),'size':u(oh-ol)})
        for mi,slot in enumerate(o.material_slots):
            if not any(t.material_index==mi for t in me.loop_triangles):continue
            rec,glass,uvname=material_record(slot.material,folder,maps,materials,lab)
            uv=me.uv_layers.get(uvname) if uvname else next((x for x in me.uv_layers if x.active_render),me.uv_layers.active)
            vs=[];ns=[];uvs=[];indices=[]
            for tri in me.loop_triangles:
                if tri.material_index!=mi:continue
                triPts=[pts[me.loops[li].vertex_index] for li in tri.loops]
                face=(triPts[1]-triPts[0]).cross(triPts[2]-triPts[0]);area=face.length*.5
                average=sum((normalMatrix@me.corner_normals[li].vector for li in tri.loops),Vector())
                okay=face.dot(average)>=0;same+=int(okay);total+=1;totalArea+=area
                if not okay:opposingArea+=area
                for li in [tri.loops[0],tri.loops[2],tri.loops[1]]:
                    p=pts[me.loops[li].vertex_index];n=(normalMatrix@me.corner_normals[li].vector).normalized()
                    vs+=u(p);ns+=u(n);uvs+=list(uv.data[li].uv) if uv else [0.,0.];indices.append(len(indices))
            assert all(math.isfinite(f) for a in (vs,ns,uvs) for f in a)
            parts.append({'name':o.name+' / '+slot.material.name,'glass':glass,'material':rec,
                          'uvLayer':uv.name if uv else '', 'vertices':vs,'normals':ns,'uv':uvs,'indices':indices})
    expected=EXPECTED[NAMES.index(name)] if lab else 11602
    assert total==expected,(name,total,expected)
    doc={'key':name[3:],'parts':parts,'shapes':[],'shapeCandidates':shapes,'size':size,
         'textureSize':1024 if not lab or int(name[:2])>=30 else 512,
         'normalOrientationScore':same/total,'sourceOpposingNormalTriangles':total-same,
         'sourceOpposingNormalAreaFraction':opposingArea/totalArea}
    out=folder/'mesh.json';out.write_text(json.dumps(doc,separators=(',',':')))
    # Probe serialization against the exact arrays that were read from native.
    reloaded=json.loads(out.read_text());assert reloaded==doc
    if lab:
        shutil.copy2(src.parent/(name+'.fbx'),folder/(name+'.fbx'))
        native=ROOT/'Native'/(name+'.blend')
        if native.exists():assert sha(native)==sourceSha,native
        else:shutil.copy2(src,native)
    else:
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs:o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,
            object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
            bake_space_transform=True,use_mesh_modifiers=False,add_leaf_bones=False,path_mode='STRIP')
    assert signature(obs)==sig
    assert sha(src)==sourceSha
    report={'key':name[3:],'source':str(src),'sourceSha256':sourceSha,
        'nativeGeometrySignature':sig,'triangles':total,'parts':len(parts),'objects':len(obs),
        'nativeBoundsMinXYZ':list(lo),'nativeBoundsMaxXYZ':list(hi),'unitySizeXYZ':size,
        'pivot':'existing bottom-center; no coordinate alteration',
        'normalOrientationScore':same/total,'sourceOpposingNormalTriangles':total-same,
        'sourceOpposingNormalAreaFraction':opposingArea/totalArea,
        'materials':{k:v[0] for k,v in materials.items()},'maps':maps,
        'checks':{'sourceBytesPreserved':True,'nativeGeometryUVSignaturePreserved':True,
                  'serializedLoopArraysExact':True,'triangles':True,'finiteGeometryUVNormals':True},
        'excludedObjects':[o.name for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('_Review_')],
        'methodology':'Harness2 methodology-only; preserve native baseline, exact transfer and channel packing probe',
        'limits':['Unity validation owned by companion agent','shapeCandidates are geometric evidence, not approved physical colliders'],
        'outputs':{p.name:sha(p) for p in folder.iterdir() if p.is_file() and p.name!='export-receipt.json' and not p.name.endswith('.meta')}}
    (folder/'export-receipt.json').write_text(json.dumps(report,indent=2));reports.append(report)
    print('EXPORTED',name,total,size,'materials',len(materials),'maps',len(maps),flush=True)
Path(__import__('tempfile').gettempdir(),'c4-cryostat-laboratory-export-receipt.json').write_text(json.dumps(reports,indent=2))
