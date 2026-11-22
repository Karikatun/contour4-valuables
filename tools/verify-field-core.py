"""Reopen native inputs and verify approved geometry/maps and the frozen runtime ZIP."""
import bpy,json,hashlib,zipfile
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[1]
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
src=root.parent/'prototype/valuable-models/meshy-field-core-15000/field-core-15000.blend'
native=root/'Native/11-field-core.blend'
report=json.loads((root/'evidence/field-core-export.json').read_text())
def read(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    points=[];uvs=[];normals=[];triangles=0
    for ob in [o for o in bpy.context.scene.objects if o.type=='MESH']:
        m=ob.data;m.calc_loop_triangles();triangles+=len(m.loop_triangles)
        points.extend(ob.matrix_world@v.co for v in m.vertices)
        uvs.extend(tuple(x.uv) for x in m.uv_layers.active.data)
        normalMatrix=ob.matrix_world.to_3x3().inverted().transposed()
        normals.extend(tuple((normalMatrix@x.vector).normalized()) for x in m.corner_normals)
    maps={im.name:hashlib.sha256(im.packed_file.data).hexdigest() for im in bpy.data.images if im.packed_file}
    return points,uvs,normals,triangles,maps
a=read(src);b=read(native)
assert a[1]==b[1] and a[3]==b[3]==14944 and a[4]==b[4]
lo=Vector(tuple(min(p[i] for p in a[0]) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in a[0]) for i in range(3)))
origin=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));scale=report['uniformScale']
error=max((q-(p-origin)*scale).length for p,q in zip(a[0],b[0]));assert error<1e-6
normalError=max(abs(p-q) for v,w in zip(a[2],b[2]) for p,q in zip(v,w));assert normalError<.0002
folder=root/'Unity/Assets/Source/11-field-core'
def pixels(name):
    im=bpy.data.images.load(str(folder/(name+'.png')),check_existing=False);im.colorspace_settings.name='Non-Color'
    return list(im.pixels)
metal=pixels('metallic');rough=pixels('roughness');mask=pixels('metal')
metalError=max(abs(mask[i]-metal[i]) for i in range(0,len(mask),4))
smoothError=max(abs(mask[i+3]-(1-rough[i])) for i in range(0,len(mask),4))
assert metalError<.004 and smoothError<.004
package=root/'dist/SpaceJourney-Contour4Valuables-0.2.3.zip'
entries=[]
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len(z.infolist())==5
    manifest=json.loads(z.read('manifest.json'));assert manifest['version_number']=='0.2.3'
    for e in z.infolist():
        data=z.read(e);digest=hashlib.sha256(data).hexdigest();entries.append(dict(path=e.filename,bytes=len(data),sha256=digest))
    assert hashlib.sha256(z.read('plugins/Contour4Valuables/Contour4Valuables.dll')).hexdigest()==sha(root/'dist/compiled/Contour4Valuables.dll')
    assert hashlib.sha256(z.read('plugins/Contour4Valuables/contour4-valuables.bundle')).hexdigest()==sha(root/'dist/bundles/contour4-valuables.bundle')
result=dict(success=True,triangles=b[3],sameUV=True,samePackedMaps=True,maxUniformVertexErrorM=error,maxCornerNormalComponentError=normalError,linearMetalMaskError=metalError,linearSmoothnessMaskError=smoothError,packageSha256=sha(package),crc='pass all five entries',entries=entries)
(root/'evidence/field-core-verification.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
