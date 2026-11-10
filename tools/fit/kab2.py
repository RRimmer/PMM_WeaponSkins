import json,itertools,numpy as np
from PIL import Image
from scipy import ndimage, optimize
M=json.load(open('/mnt/user-data/outputs/pmmws/tools/sticker_markup.json')); bydef={v['def']:(k,v) for k,v in M.items()}
KC=json.load(open('kc_their.json')); OLD=json.load(open('/tmp/claude-0/ext/kc_map_avg.json'))
def Hap(H,P):
    q=np.c_[P,np.ones(len(P))]@np.array(H).T; return q[:,:2]/q[:,2:3]
def bil(img,x,y):
    h,w=img.shape; x=np.clip(x,0,w-1.001); y=np.clip(y,0,h-1.001); x0=x.astype(int); y0=y.astype(int); fx=x-x0; fy=y-y0
    return img[y0,x0]*(1-fx)*(1-fy)+img[y0,x0+1]*fx*(1-fy)+img[y0+1,x0]*(1-fx)*fy+img[y0+1,x0+1]*fx*fy
def solve(XZ,UV):
    A=[];y=[]
    for (x,z),(u,v) in zip(XZ,UV): A+= [[-x,1,0],[-z,0,1]]; y+=[u,v]
    return np.linalg.lstsq(np.array(A),np.array(y),rcond=None)[0]
out={}
for wid in sorted(bydef):
    model,m=bydef[wid]
    if wid==2: continue
    J=json.load(open(f'img/{wid}.json')); H=np.array(KC[str(wid)]['H']); Hi=np.linalg.inv(H)
    slots={s[0]:(s[1],s[2]) for s in m['slots']}
    an=[a for a in J['anchors'] if a['i'] in slots]
    P=np.array([(a['x'],a['y']) for a in an],float); XZ=Hap(Hi,P); UV=np.array([slots[a['i']] for a in an])
    def respx(kab):
        k,a,b=kab; mz=np.c_[(a-UV[:,0])/k,(b-UV[:,1])/k]; return np.sqrt(((Hap(H,mz)-P)**2).sum(1))
    best=None
    for i,j in itertools.combinations(range(len(an)),2):
        s=solve(XZ[[i,j]],UV[[i,j]])
        if s[0]<=0: continue
        r=respx(s); inl=r<4
        if best is None or inl.sum()>best[0] or (inl.sum()==best[0] and r[inl].sum()<best[2]): best=(inl.sum(),s,r[inl].sum(),inl)
    n_in,s,_,inl=best
    s=solve(XZ[inl],UV[inl]) if inl.sum()>=2 else s
    # mask check: polygons -> model -> their px overlap with mask
    mask=np.array(Image.open(f'img/{wid}_mask.png').convert('L'))>128; Mf=ndimage.gaussian_filter(mask.astype(float),1.0)
    tris=np.array(m['tris']).reshape(-1,3,2); rng=np.random.default_rng(0); pts=[]
    for t in tris:
        r=rng.random((40,2)); r[r.sum(1)>1]=1-r[r.sum(1)>1]; pts.append(t[0]+r[:,:1]*(t[1]-t[0])+r[:,1:]*(t[2]-t[0]))
    PP=np.vstack(pts)
    def cov(kab):
        k,a,b=kab; mz=np.c_[(a-PP[:,0])/k,(b-PP[:,1])/k]; q=Hap(H,mz); return bil(Mf,q[:,0],q[:,1]).mean()
    c_anchor=cov(s)
    o=OLD.get(model); c_old=cov(o) if o else 0
    # mask-optimized from best init
    init=s if c_anchor>=c_old else np.array(o)
    r=optimize.minimize(lambda p:-cov(p)+ (0.002*np.mean(respx(p)[inl]) if n_in>=3 else 0),init,method='Nelder-Mead',options={'xatol':1e-6,'fatol':1e-6,'maxiter':4000})
    sm=r.x; c_mask=cov(sm)
    choice = s if (n_in>=3 and c_anchor>=c_mask-0.03) else sm
    out[wid]={'kab':[float(x) for x in choice],'inliers':int(n_in),'n':len(an),'cov_anchor':round(c_anchor,3),'cov_mask':round(c_mask,3),'cov_old':round(c_old,3),'src':'anchors' if choice is s else 'mask'}
    print(wid,model.ljust(26),'inl %d/%d'%(n_in,len(an)),'cov anchor %.3f mask %.3f old %.3f'%(c_anchor,c_mask,c_old),'->',out[wid]['src'],'res',np.round(respx(choice),1).tolist())
json.dump(out,open('kab2.json','w'),indent=1)
