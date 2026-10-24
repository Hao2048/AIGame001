"""Rebuild the authored office PBR textures with Blender. The active character is downloaded Renderpeople Eric."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/Realism'; OUT.mkdir(parents=True,exist_ok=True)
TEX=OUT/'Textures'; TEX.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(71)

# Seamless procedural PBR source textures, authored here rather than fetched art.
rng=np.random.default_rng(71); N=1024
u,v=np.meshgrid(np.arange(N)/N,np.arange(N)/N)
noise=rng.random((N,N))
def image(name,rgb):
    rgb=np.clip(rgb,0,1).astype(np.float32)
    if rgb.ndim==2: rgb=np.repeat(rgb[:,:,None],3,axis=2)
    rgba=np.concatenate([rgb,np.ones((N,N,1),dtype=np.float32)],axis=2)
    im=bpy.data.images.new(name,N,N,alpha=True)
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw=str(TEX/(name+'.png')); im.file_format='PNG'; im.save()
    return im
# Warped, low-contrast growth rings with irregular long fibres, no regular zebra bands.
warp=v*180+np.sin(u*9)*2.8+np.sin(u*31)*.7+np.sin(u*83)*.18
wood=.5+.055*np.sin(warp)+.022*np.sin(warp*3.71+np.sin(u*14))+.025*(noise-.5)
woodrgb=np.stack([.28+wood*.22,.12+wood*.14,.052+wood*.075],axis=2)
image('Walnut_Base',woodrgb)
weave=(np.sin(u*N*math.pi)*np.sin(v*N*math.pi)) # small-scale variation below is visible in normal.
cloth=.75+.25*noise+.05*np.sin(u*1600)*np.cos(v*1600)
image('Suit_Base',np.stack([cloth*.18,cloth*.22,cloth*.29],axis=2))
carpet=.5+noise*.30+.07*np.sin(u*3200)*np.cos(v*1600)
image('Carpet_Base',np.stack([carpet*.43,carpet*.48,carpet*.49],axis=2))
image('Plaster_Base',np.stack([.72+(noise-.5)*.028,.71+(noise-.5)*.027,.66+(noise-.5)*.027],axis=2))
image('Skin_Base',np.stack([.69+(noise-.5)*.036,.43+(noise-.5)*.027,.31+(noise-.5)*.021],axis=2))
# Tangent normals, physical fine-grained relief rather than painted shadows.
for name,h,strength in [('Walnut',wood,.07),('Suit',noise,.14),('Carpet',noise,.4),('Plaster',noise,.1),('Skin',noise,.08)]:
    dx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*strength
    dy=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*strength
    norm=np.stack([-dx,-dy,np.ones_like(dx)],axis=2);norm/=np.linalg.norm(norm,axis=2)[:,:,None]
    image(name+'_Normal',norm*.5+.5)
