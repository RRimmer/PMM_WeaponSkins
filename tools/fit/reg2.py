"""Register the reference clean render -> our inventory render (skin or base) with a homography on alpha silhouettes."""
import json,sys,numpy as np
from PIL import Image
from scipy import ndimage, optimize
R=json.load(open('/tmp/claude-0/ext/fit_in.json'))
M=json.load(open('/mnt/user-data/outputs/pmmws/tools/sticker_markup.json')); bydef={v['def']:k for k,v in M.items()}
BASE='/mnt/user-data/uploads/ServerCS/Create/cs2-WeaponPaints-build-459/website/img/weapon/'
imgset=sys.argv[1]; outp=sys.argv[2]; only=sys.argv[3].split(',') if len(sys.argv)>3 and sys.argv[3] else None
import os
out=json.load(open(outp)) if os.path.exists(outp) else {}
def ours(model):
    if imgset=='skins': return R[model]['img']
    n=model.split('_',2)[2]; n={'glock18':'glock','m4a4':'m4a1'}.get(n,n); return BASE+'weapon_%s.png'%n
def bil(img,x,y):
    h,w=img.shape; x=np.clip(x,0,w-1.001); y=np.clip(y,0,h-1.001); x0=x.astype(int); y0=y.astype(int); fx=x-x0; fy=y-y0
    return img[y0,x0]*(1-fx)*(1-fy)+img[y0,x0+1]*fx*(1-fy)+img[y0+1,x0]*(1-fx)*fy+img[y0+1,x0+1]*fx*fy
def Hap(H,P):
    q=np.c_[P,np.ones(len(P))]@H.T; return q[:,:2]/q[:,2:3]
for wid in sorted(bydef):
    if only and str(wid) not in only: continue
    model=bydef[wid]
    A=np.array(Image.open(f'img/{wid}_clean.png').convert('RGBA'))[:,:,3]>60
    B=np.array(Image.open(ours(model)).convert('RGBA'))[:,:,3]>60
    Af=ndimage.gaussian_filter(A.astype(float),1.5); Bf=ndimage.gaussian_filter(B.astype(float),1.5)
    rng=np.random.default_rng(0)
    ay,ax=np.where(A); s=rng.choice(len(ax),min(8000,len(ax)),replace=False); PA=np.c_[ax[s],ay[s]].astype(float)
    by,bx=np.where(B); s=rng.choice(len(bx),min(8000,len(bx)),replace=False); PB=np.c_[bx[s],by[s]].astype(float)
    # init: bbox similarity
    def bb(P): return P.min(0),P.max(0)
    a0,a1=bb(PA); b0,b1=bb(PB); sc=(b1-b0)/(a1-a0)
    H0=np.array([[sc[0],0,b0[0]-sc[0]*a0[0]],[0,sc[1],b0[1]-sc[1]*a0[1]],[0,0,1]])
    def cost(p):
        H=np.array(list(p)+[1.0]).reshape(3,3)
        try: Hi=np.linalg.inv(H)
        except Exception: return 5
        q=Hap(H,PA); a=bil(Bf,q[:,0],q[:,1]).mean()
        q=Hap(Hi,PB); b=bil(Af,q[:,0],q[:,1]).mean()
        return -(a+b)
    best=None
    for p0 in [H0.flatten()[:8]]:
        r=optimize.minimize(cost,p0,method='Powell',options={'maxiter':30000,'xtol':1e-7,'ftol':1e-8})
        r=optimize.minimize(cost,r.x,method='Nelder-Mead',options={'maxiter':20000,'xatol':1e-7,'fatol':1e-8})
        if best is None or r.fun<best.fun: best=r
    H=np.array(list(best.x)+[1.0]).reshape(3,3)
    out[str(wid)]={'H':H.tolist(),'score':-best.fun}
    print(wid,model,imgset,'score %.3f'%-best.fun,flush=True)
    json.dump(out,open(outp,'w'),indent=1)
