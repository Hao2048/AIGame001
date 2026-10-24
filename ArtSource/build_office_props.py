"""Detailed bevelled office furniture, hollow ceramic cup, mesh chair and botanical leaves."""
import bpy,math,random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Resources/Realism';random.seed(37)
def clear():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def m(name,c,rough=.5,metal=0):
 mat=bpy.data.materials.get('MAT-'+name)
 if mat:return mat
 mat=bpy.data.materials.new('MAT-'+name);mat.use_nodes=True
 b=mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled');o=mat.node_tree.nodes.get('Material Output') or mat.node_tree.nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(b.outputs['BSDF'],o.inputs['Surface'])
 b.inputs['Base Color'].default_value=(*c,1);b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal;mat.diffuse_color=(*c,1);return mat
walnut=m('Walnut',(.37,.19,.08));metal=m('Metal',(.6,.62,.63),.25,1);plastic=m('Plastic',(.04,.055,.06),.4)
key=m('Keycap',(.13,.15,.16));legend=m('KeyLegend',(.7,.72,.71));ceramic=m('Ceramic',(.8,.78,.7),.2);coffee=m('Coffee',(.025,.012,.003),.15)
cloth=m('Suit',(.06,.08,.10),.8);leather=m('Leather',(.03,.025,.02),.4);leafmat=m('Leaf',(.13,.3,.1));soil=m('Soil',(.05,.025,.01));paper=m('Shirt',(.85,.86,.83));tie=m('Tie',(.3,.05,.02))
def co(p):return (p[0],-p[2],p[1])
def mesh(name,vs,fs,mat):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new('GEO-'+name,me);bpy.context.collection.objects.link(o);o.data.materials.append(mat);return o
def box(name,p,size,mat,bev=.004):
 bpy.ops.mesh.primitive_cube_add(size=1,location=co(p));o=bpy.context.object;o.name='GEO-'+name;o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
 if bev:
  b=o.modifiers.new('EdgeRoundover','BEVEL');b.width=bev;b.segments=3;bpy.ops.object.modifier_apply(modifier=b.name)
  n=o.modifiers.new('AreaWeightedNormals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=n.name)
 return o
def tube(name,points,r,mat):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=3
 sp=cu.splines.new('POLY');sp.points.add(len(points)-1)
 for p,c in zip(sp.points,points):p.co=(*co(c),1)
 o=bpy.data.objects.new('GEO-'+name,cu);bpy.context.collection.objects.link(o);o.data.materials.append(mat);return o
def lathe(name,p,profile,mat,steps=64):
 vs=[];fs=[]
 for h,r in profile:
  for i in range(steps):
   a=i*math.pi*2/steps;vs.append(co((p[0]+r*math.cos(a),p[1]+h,p[2]+r*math.sin(a))))
 for j in range(len(profile)-1):
  for i in range(steps):fs.append((j*steps+i,j*steps+(i+1)%steps,(j+1)*steps+(i+1)%steps,(j+1)*steps+i))
 o=mesh(name,vs,fs,mat)
 for f in o.data.polygons:f.use_smooth=True
 return o
def export(name):
 for o in list(bpy.context.scene.objects):
  if o.type in {'CURVE','FONT'}:
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
 # Merge by material, preserving GPU-friendly shared surfaces.
 for mat in list(bpy.data.materials):
  obs=[o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.data.materials)==1 and o.data.materials[0]==mat]
  if len(obs)>1:
   bpy.ops.object.select_all(action='DESELECT')
   for o in obs:o.select_set(True)
   bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();obs[0].name='GEO-'+mat.name
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS')
 print(name,sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH'))
clear()
box('WalnutDesktop',(0,.72,-.25),(2.6,.065,1.15),walnut,.014)
box('Apron',(0,.67,-.25),(2.4,.05,.91),plastic,.006)
for side in [-1,1]:
 for z in [-.69,.19]:box('DeskLeg',(side*1.19,.345,z),(.045,.69,.045),metal,.007)
 box('DeskFoot',(side*1.19,.025,-.25),(.055,.04,1.0),plastic,.008)
box('SteelCrossbeam',(0,.56,.16),(2.38,.04,.035),metal)
box('CableTray',(0,.62,.13),(1.5,.05,.20),plastic)
box('DeskPad',(.10,.758,-.58),(1.8,.008,.44),leather,.012)
box('KeyboardCase',(-.05,.778,-.59),(.65,.025,.23),plastic,.008)
for row in range(5):
 for col in range(15):
  x=-.348+col*.0415;z=-.683+row*.041
  box('Keycap',(x,.799,z),(.036,.012,.032),key,.003)
  if row<4:
   text=bpy.data.curves.new('KeyLegend','FONT');text.body=('1234567890QWERTY' if row==0 else 'ASDFGHJKLZXCVBN')[col];text.size=.009;text.extrude=.00005;text.align_x='CENTER';text.align_y='CENTER'
   obj=bpy.data.objects.new('GEO-KeyLegend',text);bpy.context.collection.objects.link(obj);obj.location=co((x,.806,z));obj.data.materials.append(legend)
box('Spacebar',(-.02,.80,-.51),(.24,.012,.034),key,.003)
# Mouse, wheel and scroll seam.
lathe('Mouse',(.60,.765,-.61),[(0,.034),(.025,.039),(.04,.032),(.045,0)],plastic)
box('MouseSplit',(.6,.802,-.60),(.001,.002,.045),key,0)
lathe('ScrollWheel',(.6,.806,-.62),[(0,.006),(.007,.006)],metal,24)
# Hollow mug with a rolled rim and ceramic handle.
p=(-.80,.756,-.32)
lathe('Mug',p,[(0,.043),(.006,.045),(.10,.049),(.108,.048),(.11,.046),(.108,.043),(.015,.039),(.012,0)],ceramic)
lathe('Coffee',p,[(.092,0),(.092,.042)],coffee)
tube('MugHandle',[(p[0]-.045-.035*math.sin(a),p[1]+.058+.035*math.cos(a),p[2]) for a in [i*math.pi/32 for i in range(33)]],.008,ceramic)
# Real paper stack and bound notebook.
box('Notebook',(.96,.77,-.32),(.25,.024,.35),tie,.006)
for i in range(5):box('NotebookPages',(.96,.773+i*.002,-.32),(.238,.001,.337),paper,0)
box('PaperSheet',(.88,.792,-.33),(.22,.001,.30),paper,.001)
for j in range(8):box('PrintedLine',(.88,.793,-.43+j*.02),(.15 if j else .08,.0003,.001),key,0)
tube('Pen',[(.92,.806,-.43),(1.04,.806,-.26)],.004,plastic)
# Cables follow a hanging arc behind the monitor.
tube('MonitorCable',[(.04,.98,.09),(.08,.79,.2),(.15,.64,.24),(.32,.57,.25),(.6,.60,.2)],.006,plastic)
export('OfficeDesk')
clear()
# Bezel uses four frame pieces so the world-space Canvas has a true opening.
w=1.10;h=.62;cy=1.30
box('DisplayBack',(0,cy,.09),(w+.055,h+.055,.065),plastic,.016)
for x in [-1,1]:box('BezelSide',(x*(w/2+.014),cy,.045),(.028,h+.04,.027),plastic,.007)
for y in [-1,1]:box('BezelRail',(0,cy+y*(h/2+.012),.045),(w+.04,.025,.026),plastic,.006)
box('DisplayChin',(0,cy-h/2-.016,.048),(w+.04,.038,.03),plastic,.006)
box('Stand',(0,.87,.11),(.085,.23,.05),metal,.008)
box('StandBase',(0,.771,.035),(.40,.021,.24),metal,.012)
# Ventilation slots on the back; physical buttons on the lower bezel.
for i in range(32):box('BackVent',(-.43+i*.028,cy+.23,.125),(.015,.001,.001),key,0)
for i in range(4):box('ControlButton',(.37+i*.026,cy-h/2-.018,.028),(.015,.004,.004),metal,.002)
export('OfficeMonitor')
clear()
# Mesh-backed ergonomic office chair with a curved shell.
lathe('ChairColumn',(0,0,0),[(.07,.035),(.43,.03)],metal,32)
for i in range(5):
 a=i*math.pi*2/5;x=math.cos(a)*.31;z=math.sin(a)*.31
 tube('ChairStar',[(0,.10,0),(x*.6,.07,z*.6),(x,.06,z)],.023,plastic)
 lathe('Castor',(x,.015,z),[(0,.035),(.04,.035)],plastic,24)
box('Seat',(0,.47,0),(.49,.09,.46),leather,.045)
vs=[];fs=[]
for row in range(13):
 h=.55+row*.048
 for col in range(13):
  x=-.245+col*.49/12;vs.append(co((x,h,.18+.075*(x/.25)**2+.025*math.sin(row/12*math.pi))))
for r in range(12):
 for c in range(12):i=r*13+c;fs.append((i,i+1,i+14,i+13))
o=mesh('WovenBack',vs,fs,cloth);so=o.modifiers.new('ShellThickness','SOLIDIFY');so.thickness=.012
bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=so.name)
for side in [-1,1]:
 tube('ChairBackRim',[(side*.245,.55+.048*i,.26) for i in range(13)],.018,plastic)
 tube('ArmSupport',[(side*.29,.48,0),(side*.29,.69,0)],.018,metal)
 box('Armrest',(side*.29,.70,-.01),(.07,.035,.26),plastic,.015)
export('OfficeChair')
clear()
# Palm: curved, tapered leaves with a central rib, not scaled spheres.
lathe('Planter',(0,0,0),[(0,.17),(.02,.18),(.31,.22),(.33,.22),(.335,.205),(.04,.15)],ceramic)
lathe('PottingSoil',(0,0,0),[(.30,0),(.30,.20)],soil)
for i in range(34):
 a=i*2.39996;length=.55+random.random()*.75;lean=.22+random.random()*.45
 origin=Vector((.035*math.cos(a),.3,.035*math.sin(a)));tip=origin+Vector((math.cos(a)*lean,length,math.sin(a)*lean));pts=[];vs=[];fs=[]
 for step in range(15):
  t=step/14;center=origin+(tip-origin)*t;center.y-=math.sin(t*math.pi)*.035
  width=math.sin(math.pi*t)**.8*.047
  side=Vector((-math.sin(a),0,math.cos(a)))
  vs.extend([co(center+side*width),co(center+Vector((0,.007*math.sin(t*math.pi),0))),co(center-side*width)])
  pts.append(tuple(center))
 for row in range(14):
  for col in range(2):j=row*3+col;fs.append((j,j+1,j+4,j+3))
 o=mesh('PalmLeaf',vs,fs,leafmat)
 for f in o.data.polygons:f.use_smooth=True
 tube('LeafRib',pts,.0014,leafmat)
export('OfficePlant')
# A reusable rounded unit mesh for background furniture and architectural edge highlights.
clear();box('BevelledUnit',(0,0,0),(1,1,1),plastic,.035);export('BevelledUnit')
