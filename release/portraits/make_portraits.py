import sys
from PIL import Image, ImageOps
S=sys.argv[1]; OUT=sys.argv[2]
# name: (face x, face y, crop size as fraction of source, mirror so the character faces right)
SPEC={
 'VendR':(.53,.47,.85,0),'ToasterKnight':(.5,.42,.9,0),'Sentry':(.5,.42,.85,0),'SNAP':(.42,.45,.8,0),
 'Pudge':(.45,.5,.9,0),'Prophet':(.55,.4,.75,1),'Prisoner':(.5,.42,.8,0),'PigeonKnight':(.45,.52,.85,1),
 'Machina':(.5,.47,.85,0),'KennethFiretank':(.5,.47,.8,0),'Juggernaut':(.47,.45,.85,0),'Hivelancer':(.55,.5,.85,0),
 'Hardpoint':(.42,.55,.8,1),'Harbinger':(.5,.38,.85,0),'HamburgalDuo':(.5,.28,.75,0),'GunBlade':(.55,.4,.85,0),
 'DoubleD':(.48,.47,.85,1),'DJBassDrop':(.5,.43,.9,0),'Chip':(.5,.45,.8,0),'CaptainPhil':(.55,.42,.85,0),
 'Bard':(.45,.48,.85,0),'Alchemist':(.5,.3,.8,0),'AudiTechnician':(.47,.3,.75,0),'BillyBigsuits':(.52,.3,.75,0),
 'CarMaxSpecialist':(.5,.15,.7,0),'MommyMilkers':(.6,.3,.75,0),'HardpointNext':(.42,.55,.8,1),'GravitosNext':(.47,.15,.6,0),
 'BerserkFury':(.5,.45,1,0),'Surveyor':(.53,.38,.9,0),'PhantomAssassin':(.47,.45,.85,0),'Reaper':(.66,.4,.85,0),
 'ClayRebel':(.55,.42,.9,0),'Emberwarden':(.6,.3,.9,0),'AshwingDrake':(.47,.36,.7,1),'Duelist':(.57,.33,.75,0),
 'InfestedGeneral':(.45,.33,.7,0),'Nidus':(.43,.47,.6,0),'OatsBrother':(.65,.45,.75,0),'Gravitos':(.53,.32,.9,0),
 'WilburMod':(.5,.5,.95,0),
}
TX,TY=.5,.42   # where the face lands in the card picture
for n,(fx,fy,s,flip) in SPEC.items():
    im=Image.open(f'{S}/src/{n}.png').convert('RGBA')
    if flip: im=ImageOps.mirror(im); fx=1-fx
    W,H=im.size; cs=s*W
    x0=fx*W-TX*cs; y0=fy*H-TY*cs
    # keep inside the picture where possible; allow at most 12% empty edge so the face stays near the centre
    pad=.12*cs
    x0=min(max(x0,-pad),W-cs+pad); y0=min(max(y0,-pad),H-cs+pad)
    out=Image.new('RGBA',(int(cs),int(cs)),(0,0,0,0))
    out.alpha_composite(im.crop((int(x0),int(y0),int(x0+cs),int(y0+cs))).convert('RGBA') if False else Image.new('RGBA',(1,1),(0,0,0,0)))
    box=(max(0,int(x0)),max(0,int(y0)),min(W,int(x0+cs)),min(H,int(y0+cs)))
    out.alpha_composite(im.crop(box),(box[0]-int(x0),box[1]-int(y0)))
    out.resize((512,512),Image.LANCZOS).save(f'{OUT}/{n}.png',optimize=True)
