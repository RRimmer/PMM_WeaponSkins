import json,itertools,numpy as np
M=json.load(open('/mnt/user-data/outputs/pmmws/tools/sticker_markup.json')); bydef={v['def']:(k,v) for k,v in M.items()}
KC=json.load(open('kc_their.json')); RS=json.load(open('reg_skins.json')); RB=json.load(open('reg_base.json'))
p='/mnt/user-data/outputs/pmmws/tools/sticker_fit.json'; F=json.load(open(p))
def Hap(H,P):
    q=np.c_[P,np.ones(len(P))]@np.array(H).T; return q[:,:2]/q[:,2:3]
def solve(XZ,UV):
    A=[];y=[]
    for (x,z),(u,v) in zip(XZ,UV): A+= [[-x,1,0],[-z,0,1]]; y+=[u,v]
    return np.linalg.lstsq(np.array(A),np.array(y),rcond=None)[0]
for wid in sorted(bydef):
    model,m=bydef[wid]
    for key in ('kab','Hc','R'): F[model].pop(key,None)
    F[model].pop('kc',None)
    if wid==2 or str(wid) not in RS or str(wid) not in RB: print('skip',wid,model); continue
    J=json.load(open(f'img/{wid}.json')); H=np.array(KC[str(wid)]['H']); Hi=np.linalg.inv(H)
    slots={s[0]:(s[1],s[2]) for s in m['slots']}
    an=[a for a in J['anchors'] if a['i'] in slots]
    P=np.array([(a['x'],a['y']) for a in an],float); XZ=Hap(Hi,P); UV=np.array([slots[a['i']] for a in an])
    def respx(s):
        k,a,b=s; mz=np.c_[(a-UV[:,0])/k,(b-UV[:,1])/k]; return np.sqrt(((Hap(H,mz)-P)**2).sum(1))
    best=None
    for i,j in itertools.combinations(range(len(an)),2):
        s=solve(XZ[[i,j]],UV[[i,j]])
        if s[0]<=0: continue
        r=respx(s); inl=r<4
        if best is None or inl.sum()>best[0] or (inl.sum()==best[0] and r[inl].sum()<best[2]): best=(inl.sum(),s,r[inl].sum(),inl)
    s=solve(XZ[best[3]],UV[best[3]])
    F[model]['kab']=[round(float(x),6) for x in s]
    F[model]['Hc']=[[float(x) for x in r] for r in H]
    F[model]['R']={'skins':RS[str(wid)]['H'],'base':RB[str(wid)]['H']}
    print(wid,model,'kab',np.round(s,4),'inliers',int(best[0]),'/',len(an),'reg',round(RS[str(wid)]['score'],3),round(RB[str(wid)]['score'],3))
json.dump(F,open(p,'w'),indent=1)
