"""Original code-drawn game icon. No external images or font files."""
from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[1];out=root/'Assets/Branding';out.mkdir(exist_ok=True)
k=2;im=Image.new('RGBA',(1024*k,1024*k));d=ImageDraw.Draw(im)
def rect(box,fill,r=0,width=0,outline=None):
 box=tuple(int(v*k) for v in box)
 if r:d.rounded_rectangle(box,radius=int(r*k),fill=fill,outline=outline,width=width*k)
 else:d.rectangle(box,fill=fill,outline=outline,width=width*k)
def line(points,fill,width):d.line([(int(x*k),int(y*k)) for x,y in points],fill=fill,width=width*k)
navy='#112737';gold='#ffd55b';mint='#73efc3';cream='#e9f1df'
rect((70,70,954,954),navy,185)
rect((166,260,858,733),gold,40)
rect((196,291,828,700),'#172f40',20)
rect((196,291,828,352),'#315466',20)
for x in [225,266,307]:rect((x,311,x+19,330),cream,3)
rect((528,373,800,671),cream,10)
for y in range(407,662,51):line([(548,y),(780,y)],'#90b7ae',8)
for x in [620,705]:line([(x,395),(x,649)],'#90b7ae',8)
rect((549,391,778,419),'#2b8973',3)
for x,y in [(264,524),(360,484),(433,542)]:
 rect((x-19,y-28,x+19,y),gold,3);rect((x-29,y+5,x+29,645),'#e88c6b',5)
line([(359,442),(359,470)],mint,9);line([(359,495),(359,522)],mint,9)
line([(319,482),(346,482)],mint,9);line([(374,482),(401,482)],mint,9)
rect((455,735,570,798),'#466777',9);rect((350,794,675,834),'#466777',17)
d.ellipse((744*k,143*k,916*k,315*k),fill=gold)
rect((818,177,843,243),navy,7);rect((818,260,843,282),navy,5)
im=im.resize((1024,1024),Image.Resampling.LANCZOS);im.save(out/'AppIcon.png')
print(out/'AppIcon.png')
