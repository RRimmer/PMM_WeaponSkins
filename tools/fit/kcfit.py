import json,sys,numpy as np
M=json.load(open('/mnt/user-data/outputs/pmmws/tools/sticker_markup.json')); bydef={v['def']:(k,v) for k,v in M.items()}
def fitH(src,dst,w=None):
    A=[]
    for (x,y),(u,v) in zip(src,dst):
        A.append([x,y,1,0,0,0,-u*x,-u*y,-u]); A.append([0,0,0,x,y,1,-v*x,-v*y,-v])
    _,_,Vt=np.linalg.svd(np.array(A)); H=Vt[-1].reshape(3,3); return H/H[2,2]
def Hap(H,P):
    q=np.c_[P,np.ones(len(P))]@H.T; return q[:,:2]/q[:,2:3]
out={}
for wid in sorted(bydef):
    model,m=bydef[wid]; J=json.load(open(f'img/{wid}.json'))
    kc=m['kc']; tq=J['quads']
    if len(kc)!=len(tq): print(wid,model,'count mismatch',len(kc),len(tq)); continue
    src=[];dst=[];w3=[]
    for q,t in zip(kc,tq):
        c=[(q[i],q[i+1],q[i+2]) for i in range(0,12,3)]
        order=[0,1,3,2]
        for k,ti in zip(order,range(4)):
            src.append((c[k][0],c[k][2])); dst.append(tuple(t[ti])); w3.append(c[k][1])
    src=np.array(src); dst=np.array(dst)
    ok=(np.abs(dst)<3000).all(1)
    # robust: iterative trimming
    sel=ok.copy()
    for it in range(4):
        H=fitH(src[sel],dst[sel]); e=np.sqrt(((Hap(H,src)-dst)**2).sum(1))
        thr=max(4.0,np.percentile(e[sel],80)*2); sel=ok&(e<thr)
    e=np.sqrt(((Hap(H,src)-dst)**2).sum(1))
    out[wid]={'H':H.tolist(),'med':float(np.median(e[sel])),'n':int(sel.sum()),'N':len(src)}
    print(wid,model.ljust(26),'inliers %d/%d median err %.2f p90 %.2f'%(sel.sum(),len(src),np.median(e[sel]),np.percentile(e[sel],90)))
json.dump(out,open('kc_their.json','w'),indent=1)
