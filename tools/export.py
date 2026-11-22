import bpy,sys,json,shutil,hashlib
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[1]
source=root.parent/'prototype/valuable-models/textured'
batch2='--batch2' in sys.argv
if batch2:source=root.parent/'prototype/valuable-models/batch-02/textured'
names=['05-portable-gravimeter','06-gimbal-gyroscope','07-gravity-lens','08-mobius-strip'] if batch2 else ['01-experiment-cassette','02-control-sample','03-sensor-rod','04-calibration-weight']
reports=[]
for name in names:
    folder=root/'Unity/Assets/Source'/name; folder.mkdir(parents=True,exist_ok=True)
    native=root/'Native'/(name+'.blend')
    src=native if native.exists() else source/name/(name+'.blend')
    if batch2 and not native.exists():shutil.copy2(src,native);src=native
    bpy.ops.wm.open_mainfile(filepath=str(src))
    objects=sorted([o for o in bpy.context.scene.objects if o.type=='MESH'],key=lambda x:x.name)
    parts=[]; shapes=[]
    for ob in objects:
        me=ob.data; me.calc_loop_triangles(); uv=me.uv_layers.active.data
        points=[ob.matrix_world@v.co for v in me.vertices]
        pts=[Vector((p.x,p.z,p.y)) for p in points]
        lo=[min(p[a] for p in pts) for a in range(3)]; hi=[max(p[a] for p in pts) for a in range(3)]
        shapes.append({'name':ob.name,'center':[(a+b)/2 for a,b in zip(lo,hi)],'size':[b-a for a,b in zip(lo,hi)]})
        for mi,slot in enumerate(ob.material_slots):
            mat=slot.material
            if not mat:continue
            glass='glass' in mat.name.lower() or 'lens' in mat.name.lower() and ('optical' in mat.name.lower() or 'transmission' in mat.name.lower())
            vs=[]; ns=[]; uvs=[]; indices=[]
            normal_matrix=ob.matrix_world.to_3x3().inverted().transposed()
            for tri in me.loop_triangles:
                if tri.material_index!=mi:continue
                # Blender -> Unity is a handedness flip. Reverse triangle order.
                for li in [tri.loops[0],tri.loops[2],tri.loops[1]]:
                    p=points[me.loops[li].vertex_index]; n=normal_matrix@me.corner_normals[li].vector;n.normalize()
                    vs.extend((p.x,p.z,p.y));ns.extend((n.x,n.z,n.y));uvs.extend(uv[li].uv);indices.append(len(indices))
            if indices:parts.append({'name':ob.name+' / '+mat.name,'glass':glass,'vertices':vs,'normals':ns,'uv':uvs,'indices':indices})
    doc={'key':name[3:],'parts':parts,'shapes':shapes}
    (folder/'mesh.json').write_text(json.dumps(doc,separators=(',',':')))
    for texture in ['color.png','normal.png','metal.png']:
        original=source/name/texture
        if original.exists():shutil.copy2(original,folder/texture)
        elif not (folder/texture).is_file():raise FileNotFoundError('Missing preserved atlas '+str(folder/texture))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP')
    reports.append({'key':name[3:],'source':str(src),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'parts':len(parts),'triangles':sum(len(p['indices'])//3 for p in parts),'shapes':shapes})
(root/'evidence'/('export-batch2.json' if batch2 else 'export.json')).write_text(json.dumps(reports,indent=2))
print('EXPORTED',len(reports),flush=True)
