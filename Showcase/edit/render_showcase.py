#!/usr/bin/env python3
"""Edit the real Unity capture into a short comedy trailer. No external footage or music."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import subprocess, json, math, wave, concurrent.futures, argparse, os, shutil

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Showcase'; RAW=OUT/'raw'; GEN=OUT/'edit/generated'; GEN.mkdir(exist_ok=True)
FFMPEG=shutil.which('ffmpeg') or '/opt/homebrew/bin/ffmpeg'; PROBE=shutil.which('ffprobe') or '/opt/homebrew/bin/ffprobe'
FONT=os.environ.get('SHOWCASE_FONT','/System/Library/Fonts/Hiragino Sans GB.ttc')
W,H,FPS=1920,1080,30
INK=(12,23,33); PAPER=(248,246,226); MINT=(116,241,198); GOLD=(255,213,91); CORAL=(255,124,107)

def font(size):return ImageFont.truetype(FONT,size)
def run(args):
 p=subprocess.run([FFMPEG,'-y','-hide_banner','-loglevel','error',*map(str,args)],capture_output=True,text=True)
 if p.returncode:raise RuntimeError(p.stderr)

def text(draw,xy,value,size,color,anchor='mm',stroke=1):
 draw.text(xy,value,font=font(size),fill=color,anchor=anchor,stroke_width=stroke,stroke_fill=color)

def caption(name,words,color,hook=False):
 if hook:
  image=Image.new('RGBA',(1760,250));d=ImageDraw.Draw(image)
  d.rounded_rectangle((0,0,1760,250),radius=28,fill=(*INK,235))
  text(d,(880,55),'上班摸鱼，最怕空气突然安静。',43,PAPER)
  text(d,(880,157),words,100,color,stroke=2)
 else:
  f=font(61);width=min(1720,int(f.getlength(words))+110);height=112
  image=Image.new('RGBA',(width,height));d=ImageDraw.Draw(image)
  d.rounded_rectangle((0,0,width-1,height-1),radius=19,fill=(*INK,236),outline=(*color,110),width=2)
  d.rounded_rectangle((0,13,8,height-13),radius=4,fill=(*color,255))
  text(d,(width/2,height/2-2),words,61,color)
 path=GEN/(name+'.png');image.save(path);return path

def end_card():
 image=Image.new('RGB',(W,H),INK);d=ImageDraw.Draw(image)
 for x in range(0,W,80):d.line((x,0,x,H),fill=(18,32,43),width=1)
 for y in range(0,H,80):d.line((0,y,W,y),fill=(18,32,43),width=1)
 d.rounded_rectangle((80,72,485,133),radius=12,fill=(30,53,63))
 text(d,(282,101),'UNITY · 实机演示',30,MINT)
 text(d,(W/2,345),'老板来了',210,GOLD,stroke=3)
 d.rectangle((724,493,1196,503),fill=MINT)
 text(d,(W/2,585),'摸鱼不是技术，是演技。',66,PAPER)
 text(d,(W/2,705),'办公室摸鱼模拟器',40,(166,190,194))
 for x,words in [(460,'空格切屏'),(960,'极限摸鱼'),(1460,'随机回头')]:
  d.rounded_rectangle((x-200,825,x+200,917),radius=18,fill=(27,46,57),outline=(55,81,91),width=2)
  text(d,(x,868),words,42,PAPER)
 text(d,(W/2,1003),'再打一枪，就下班。',30,(135,161,169))
 p=GEN/'end-card.png';image.save(p);return p

metadata=json.loads((RAW/'complete.json').read_text());assert metadata['reason']=='complete' and metadata['encoderExit']==0
E={x['name']:x['time'] for x in metadata['events']}
# Each source clip is an existing gameplay event. The hook is an editorial close-up and repeats later.
segments=[
 dict(id='hook',start=E['state_Lecture']+.10,end=E['state_Leave']-.05,speed=.65,words='老板，听我解释。',color=GOLD,crop=(200,175,1200,675),hook=True),
 dict(id='shoot',start=.50,end=E['boss_walk']-.03,speed=1,words='带薪开火，才叫上班。',color=MINT),
 dict(id='phone',start=E['phone']+.07,end=E['state_Patrol'] if E['state_Patrol']>E['phone'] else 6.3,speed=1,words='老板接电话，我接着打。',color=MINT),
 dict(id='save',start=E['save_warn'],end=E['work'],speed=.90,words='空格一按，热爱工作。',color=MINT,bottom=True),
 dict(id='work',start=E['work'],end=E['back_to_shooting']-.03,speed=.78,words='这是第 7 个「最终版」。',color=GOLD,crop=(420,255,800,450),bottom=True),
 dict(id='back',start=E['back_to_shooting'],end=E['greed'],speed=1,words='他一转身，我就开枪。',color=MINT),
 dict(id='greed',start=E['greed'],end=E['state_Approach'],speed=.90,words='再打一枪，就一枪。',color=GOLD),
 dict(id='caught',start=E['state_Approach'],end=E['state_Lecture'],speed=.90,words='完了，周末没了。',color=CORAL,bottom=True),
 dict(id='lecture',start=E['state_Lecture'],end=E['state_Leave'],speed=.90,words='老板：你屏幕上这个，是什么？',color=PAPER,bottom=True),
 dict(id='leave',start=E['state_Leave'],end=E['back_again'],speed=2.2,words='好的老板，下次一定。',color=GOLD),
 dict(id='again',start=E['back_again'],end=metadata['duration']-.03,speed=1,words='（现在继续）',color=MINT),
]
# The dictionary retains the last patrol event, so identify the first post-phone transition explicitly.
segments[2]['end']=next(x['time'] for x in metadata['events'] if x['name']=='state_Patrol' and x['time']>E['phone'])

cursor=0
for s in segments:
 s['frames']=round((s['end']-s['start'])/s['speed']*FPS);s['duration']=s['frames']/FPS;s['timelineStart']=cursor
 cursor+=s['duration'];s['caption']=caption(s['id'],s['words'],s['color'],s.get('hook',False))


def render_segment(s):
 out=GEN/(s['id']+'.mp4');dur=s['duration'];speed=s['speed']
 crop=s.get('crop');vf=f'trim=start={s["start"]}:end={s["end"]},setpts=(PTS-STARTPTS)/{speed},'
 if crop:vf+=f'crop={crop[2]}:{crop[3]}:{crop[0]}:{crop[1]},'
 vf+=f'scale={W}:{H}:flags=lanczos,fps={FPS},setsar=1[v]'
 pos='H-h-40' if s.get('bottom') else '64' if s.get('hook') else '177'
 fade=f'fade=t=in:st=0:d=0.10:alpha=1,fade=t=out:st={max(0,dur-.10)}:d=0.10:alpha=1'
 graph=f'[0:v]{vf};[2:v]format=rgba,{fade}[cap];[v][cap]overlay=x=(W-w)/2:y={pos}:shortest=1:format=auto,format=yuv420p[outv];'
 # atempo's 2x maximum is handled as a chain for the accelerated exit.
 tempo=f'atempo={speed}' if speed<=2 else 'atempo=2,atempo='+str(speed/2)
 graph+=f'[1:a]atrim=start={s["start"]}:end={s["end"]},asetpts=PTS-STARTPTS,{tempo},afade=t=in:d=0.02,afade=t=out:st={max(0,dur-.025)}:d=0.025,apad[outa]'
 run(['-i',RAW/'gameplay.mp4','-i',RAW/'gameplay.wav','-loop','1','-framerate',FPS,'-i',s['caption'],'-filter_complex',graph,'-map','[outv]','-map','[outa]','-t',dur,'-c:v','libx264','-preset','fast','-crf','18','-threads','2','-c:a','aac','-b:a','192k','-ar','48000','-ac','2',out])
 print('Rendered',s['id'],f'{dur:.2f}s',flush=True);return out

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--only',nargs='*',choices=[s['id'] for s in segments]);args=parser.parse_args()
 selected=[s for s in segments if args.only is None or s['id'] in args.only]
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(render_segment,selected))
 end=end_card();end_path=GEN/'end.mp4';end_dur=3.0
 run(['-loop','1','-framerate',FPS,'-i',end,'-f','lavfi','-i','anullsrc=r=48000:cl=stereo','-t',end_dur,'-vf','fade=t=in:d=0.15,fade=t=out:st=2.65:d=0.35,format=yuv420p','-c:v','libx264','-preset','fast','-crf','18','-c:a','aac','-b:a','192k','-ar','48000','-ac','2',end_path])
 concat=GEN/'concat.txt';concat.write_text(''.join("file '"+str(GEN/(s['id']+'.mp4'))+"'\n" for s in segments)+"file '"+str(end_path)+"'\n")
 run(['-f','concat','-safe','0','-i',concat,'-c','copy',GEN/'cut.mp4'])
 duration=cursor+end_dur
 # Original procedural comedy bed: a restrained plucked pentatonic phrase and soft percussion.
 sr=48000;n=math.ceil(duration*sr);music=np.zeros(n,dtype=np.float64);rng=np.random.default_rng(20261024);beat=60/112
 notes=[72,76,79,76,74,76,81,79]
 def add(at,wave):
  i=int(at*sr);length=min(len(wave),n-i)
  if length>0 and i>=0:music[i:i+length]+=wave[:length]
 for k in range(math.ceil(duration/beat)):
  at=k*beat
  if at<segments[0]['duration']-.1:continue
  t=np.arange(int(.42*sr))/sr;frequency=440*2**((notes[k%len(notes)]-69)/12)
  pluck=(np.sin(2*np.pi*frequency*t)+.25*np.sin(4*np.pi*frequency*t))*np.exp(-t*12)*(1-np.exp(-t*1200))
  add(at,pluck*.065)
  if k%2==0:
   t=np.arange(int(.16*sr))/sr;kick=np.sin(2*np.pi*(55*t+8*(1-np.exp(-t*28))))*np.exp(-t*32);add(at,kick*.12)
  t=np.arange(int(.045*sr))/sr;hat=rng.normal(0,1,len(t))*np.exp(-t*110);add(at+beat/2,hat*.012)
 # A brief elastic sting on the hard cut out of the hook; generated here, not sampled from a song.
 t=np.arange(int(.30*sr))/sr;boing=np.sin(2*np.pi*(190*t+35*t*t))*np.exp(-t*17);add(segments[0]['duration']-.10,boing*.13)
 fade=np.minimum(np.arange(n)/(sr*.10),1)*np.minimum((n-np.arange(n))/(sr*.35),1);music*=fade
 stereo=np.stack([music,music*.97],axis=1);stereo=np.clip(stereo,-1,1)
 with wave.open(str(GEN/'music.wav'),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(sr);w.writeframes((stereo*32767).astype('<i2').tobytes())
 final=OUT/'boss-coming-showcase.mp4'
 run(['-i',GEN/'cut.mp4','-i',GEN/'music.wav','-filter_complex','[0:a]volume=.82[game];[1:a]volume=.42[bed];[game][bed]amix=inputs=2:duration=first:normalize=0,alimiter=limit=.94,loudnorm=I=-16:TP=-1.5:LRA=9[a]','-map','0:v','-map','[a]','-c:v','copy','-c:a','aac','-b:a','192k','-ar','48000','-movflags','+faststart','-metadata','title=老板来了｜办公室摸鱼实机演示','-metadata','comment=Curated real Unity gameplay. Original procedural music. Renderpeople model credit. No external music.',final])
 plan=[{k:v for k,v in s.items() if k not in ('caption','color')} for s in segments]
 (OUT/'edit/timeline.json').write_text(json.dumps({'duration':duration,'fps':FPS,'width':W,'height':H,'segments':plan},ensure_ascii=False,indent=2))
 probe=subprocess.check_output([PROBE,'-v','error','-show_streams','-show_format','-of','json',str(final)],text=True);(OUT/'edit/final-probe.json').write_text(probe)
 print('FINAL',final,'duration',duration,flush=True)
