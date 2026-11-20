import bpy, math, sys, json
from pathlib import Path
from mathutils import Vector
base=Path(sys.argv[sys.argv.index('--')+1]);base.mkdir(parents=True,exist_ok=True)

def reset():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.scene.unit_settings.system='METRIC'
 bpy.context.scene.unit_settings.scale_length=1.0

def mat(name,color=(1,1,1,1),image=None):
 m=bpy.data.materials.new(name);m.use_nodes=True
 bsdf=m.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=color
 bsdf.inputs['Roughness'].default_value=.8
 if image:
  tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
  m.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
 return m

def checker():
 im=bpy.data.images.new('CheckerPNG',width=32,height=32)
 pixels=[]
 for y in range(32):
  for x in range(32):pixels.extend((.8,.18,.04,1) if (x//4+y//4)%2 else (.1,.55,.9,1))
 im.pixels=pixels;im.file_format='PNG';im.filepath_raw=str(base/'checker.png');im.save()
 return im

def cube(name='Part',location=(0,0,0)):
 bpy.ops.mesh.primitive_cube_add(size=1,location=location)
 ob=bpy.context.object;ob.name=name;ob.data.name=name+'_MeshData';return ob

def export(name,**settings):
 if name not in ['StaticTextured','MultiMaterial','SkinnedAnimated','ShapeKeys']:return
 for fmt,ext in [('GLB','.glb')]:
  bpy.ops.export_scene.gltf(filepath=str(base/(name+ext)),export_format=fmt,**settings)
 print('SCNET_EXPORTED',name,flush=True)

reset();im=checker();m=mat('CheckerMaterial',image=im)
parent=bpy.data.objects.new('ParentPivot',None);bpy.context.collection.objects.link(parent)
parent.rotation_euler=(0,0,math.radians(35));parent.location=(.25,0,.5)
a=cube('VisiblePart',(0,0,.5));a.parent=parent;a.data.materials.append(m)
b=bpy.data.objects.new('LinkedPart',a.data);bpy.context.collection.objects.link(b)
b.location=(-1.2,.2,.4);b.scale=(-.7,.5,1.1)
export('StaticTextured')

reset();a=cube('Untextured')
while a.data.uv_layers:a.data.uv_layers.remove(a.data.uv_layers[0])
a.data.materials.append(mat('SolidColor',(.1,.65,.3,1)))
export('Untextured')

reset();a=cube('NoNormals');a.data.materials.append(mat('CheckerMaterial',image=checker()))
export('NoNormals',export_normals=False)

reset();a=cube('MultiMaterial');a.data.materials.append(mat('Red',(0.8,.05,.05,1)));a.data.materials.append(mat('Blue',(.05,.15,.8,1)))
for face in a.data.polygons:face.material_index=face.index%2
export('MultiMaterial')

reset();a=cube('SkinnedBody',(0,0,1));a.scale=(.4,.4,2)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
a.data.materials.append(mat('CheckerMaterial',image=checker()))
arm_data=bpy.data.armatures.new('RigData');rig=bpy.data.objects.new('Rig',arm_data);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);a.select_set(False)
bpy.ops.object.mode_set(mode='EDIT')
root=arm_data.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,1)
tip=arm_data.edit_bones.new('Tip');tip.head=(0,0,1);tip.tail=(0,0,2);tip.parent=root
bpy.ops.object.mode_set(mode='OBJECT')
a.parent=rig;mod=a.modifiers.new('Armature','ARMATURE');mod.object=rig
v0=a.vertex_groups.new(name='Root');v1=a.vertex_groups.new(name='Tip')
for v in a.data.vertices:(v0 if v.co.z<0 else v1).add([v.index],1.0,'REPLACE')
p=rig.pose.bones['Tip'];p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.keyframe_insert(data_path='rotation_euler',frame=1)
p.rotation_euler=(.7,0,0);p.keyframe_insert(data_path='rotation_euler',frame=20)
bpy.context.scene.frame_end=20;bpy.context.scene.frame_set(1)
export('SkinnedAnimated')

reset();a=cube('ShapeKeyPart');a.data.materials.append(mat('CheckerMaterial',image=checker()))
a.shape_key_add(name='Basis');key=a.shape_key_add(name='Stretch')
for v in key.data:v.co.z*=1.8
key.value=.5
export('ShapeKeys')

(base/'checker.png').unlink(missing_ok=True)
