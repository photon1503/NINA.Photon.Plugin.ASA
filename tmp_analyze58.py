import math
from collections import defaultdict

path = r'doc/Grid 58Stars.grd'
lat_deg = 48.0
phi = math.radians(lat_deg)

with open(path,'r',encoding='utf-8') as f:
    lines=[ln.strip() for ln in f if ln.strip()!='']

n=int(lines[0])
pts=[]
idx=1
for i in range(n):
    az=float(lines[idx]); alt=float(lines[idx+1]);
    is_mouse=lines[idx+2].strip('"').lower()=='true'
    only_slew=lines[idx+3].strip('"').lower()=='true'
    pier=int(lines[idx+4])
    idx+=5
    if only_slew:
        continue
    A=az
    h=alt
    sin_h=math.sin(h); cos_h=math.cos(h)
    sinA=math.sin(A); cosA=math.cos(A)
    sin_dec = sin_h*math.sin(phi) + cos_h*math.cos(phi)*cosA
    sin_dec=max(-1,min(1,sin_dec))
    dec=math.asin(sin_dec)
    cos_dec=max(1e-14,math.cos(dec))
    sinH = -(sinA*cos_h)/cos_dec
    cosH = (sin_h - math.sin(phi)*sin_dec)/(math.cos(phi)*cos_dec)
    H=math.atan2(sinH,cosH)
    Hdeg=math.degrees(H)
    decdeg=math.degrees(dec)
    pts.append((decdeg,Hdeg,math.degrees(A),math.degrees(h),pier,is_mouse))

# cluster declination values by nearest 0.75 deg
pts_sorted=sorted(pts,key=lambda t:t[0])
clusters=[]
for p in pts_sorted:
    d=p[0]
    if not clusters:
        clusters.append([p])
    else:
        mean=sum(x[0] for x in clusters[-1])/len(clusters[-1])
        if abs(d-mean)<=0.75:
            clusters[-1].append(p)
        else:
            clusters.append([p])

print(f'points={len(pts)} clusters={len(clusters)}')
print('ring summary: dec_mean,count,HA_step_stats')
for c in clusters:
    decs=[x[0] for x in c]
    has=sorted([x[1] for x in c])
    # circular diffs
    diffs=[]
    for i in range(len(has)):
        a=has[i]
        b=has[(i+1)%len(has)]
        d=(b-a) % 360.0
        diffs.append(d)
    if len(has)>1:
        mean_step=sum(diffs)/len(diffs)
        spread=max(diffs)-min(diffs)
        std=(sum((x-mean_step)**2 for x in diffs)/len(diffs))**0.5
    else:
        mean_step=0; spread=0; std=0
    print(f'{sum(decs)/len(decs):8.3f}  {len(c):2d}  meanStep={mean_step:7.3f}  spread={spread:7.3f}  std={std:7.3f}')

# show declination spacings between ring means
means=[sum(x[0] for x in c)/len(c) for c in clusters]
print('\nring mean dec spacings:')
for i in range(1,len(means)):
    print(f'{means[i]-means[i-1]:7.3f}',end=' ')
print('\n')

# least-squares fit of ring count vs cos(dec)^p form: log(count)=logK + p*log(cos(dec))
xs=[]; ys=[]
for c in clusters:
    m=sum(x[0] for x in c)/len(c)
    cnt=len(c)
    cosv=abs(math.cos(math.radians(m)))
    if cnt>0 and cosv>1e-6:
        xs.append(math.log(cosv)); ys.append(math.log(cnt))
if len(xs)>=2:
    xbar=sum(xs)/len(xs); ybar=sum(ys)/len(ys)
    num=sum((x-xbar)*(y-ybar) for x,y in zip(xs,ys))
    den=sum((x-xbar)**2 for x in xs)
    p=num/den if den else float('nan')
    K=math.exp(ybar-p*xbar)
    print(f'fit count ≈ {K:.3f} * cos(dec)^{p:.3f}')
