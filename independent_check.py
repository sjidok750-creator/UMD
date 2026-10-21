"""Independent verifier: adaptive quadrature and Newton equations, no PMStudio imports.
Usage: python independent_check.py independent-states.json output.csv
Only reuses exported input data and numerical results, not the C# clipping/root code.
"""
import csv,json,math,sys
from pathlib import Path

def integrate_smooth(fn,lo,hi,tol=1e-13):
    def simpson(a,b):return (b-a)/6*(fn(a)+4*fn((a+b)/2)+fn(b))
    def recur(a,b,s,t,n):
        mid=(a+b)/2;left=simpson(a,mid);right=simpson(mid,b)
        if n==0 or abs(left+right-s)<15*t:return left+right+(left+right-s)/15
        return recur(a,mid,left,t/2,n-1)+recur(mid,b,right,t/2,n-1)
    return recur(lo,hi,simpson(lo,hi),tol,22)

def bisect(fn,lo,hi):
    f=fn(lo)
    assert f*fn(hi)<0
    for _ in range(65):
        mid=(lo+hi)/2
        if fn(mid)*f>0:lo=mid
        else:hi=mid
    return (lo+hi)/2

data=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
records=[]
def evaluate(p,angle,c):
    m=p['Material'];nx=math.cos(math.radians(angle));ny=math.sin(math.radians(angle));r=p.get('CircularRadius',0)
    points=p['Contours'][0]['Points'];cx=sum(v['X'] for v in points)/len(points);cy=sum(v['Y'] for v in points)/len(points)
    xmax=max(v['X']-cx for v in points);xmin=min(v['X']-cx for v in points);ymax=max(v['Y']-cy for v in points);ymin=min(v['Y']-cy for v in points)
    support=r if r else max(nx*(v['X']-cx)+ny*(v['Y']-cy) for v in points)
    level=support-m['Beta']*c
    if r:
        low=max(-r,min(r,level))
        theta=math.acos(low/r)
        a=2*r*r*integrate_smooth(lambda t:math.sin(t)**2,0,theta)
        first=2*r**3*integrate_smooth(lambda t:math.cos(t)*math.sin(t)**2,0,theta)
        qx=nx*first;qy=ny*first
    else:
        def bounds(y):
            if abs(nx)<1e-12:return (xmin,xmax) if ny*y>=level else (0,0)
            edge=(level-ny*y)/nx
            lo,hi=(max(xmin,edge),xmax) if nx>0 else (xmin,min(xmax,edge))
            return (lo,hi) if hi>lo else (0,0)
        breaks=[ymin,ymax]
        if abs(ny)>1e-12:
            breaks.extend((level-nx*x)/ny for x in [xmin,xmax] if ymin<(level-nx*x)/ny<ymax)
        def integrate(fn):
            return sum((hi-lo)/6*(fn(lo)+4*fn((lo+hi)/2)+fn(hi)) for lo,hi in zip(sorted(breaks)[:-1],sorted(breaks)[1:]))
        a=integrate(lambda y:bounds(y)[1]-bounds(y)[0])
        qx=integrate(lambda y:(bounds(y)[1]**2-bounds(y)[0]**2)/2)
        qy=integrate(lambda y:y*(bounds(y)[1]-bounds(y)[0]))
    gross_c=m['Alpha']*m['Fc']*a
    force=gross_c;mx=m['Alpha']*m['Fc']*qy;my=-m['Alpha']*m['Fc']*qx
    strains=[]
    for b in p['Bars']:
        x,y=b['X']-cx,b['Y']-cy;depth=support-nx*x-ny*y
        strain=m['EpsCu']*(1-depth/c);strains.append(strain)
        stress=max(-m['Fy'],min(m['Fy'],m['Es']*strain))
        if nx*x+ny*y>=level:stress-=m['Alpha']*m['Fc']
        f=stress*b['Area'];force+=f;mx+=f*y;my-=f*x
    return force/1000,mx/1e6,my/1e6,min(strains)

def compare(name,p,point):
    actual=evaluate(p,point['Angle'],point['Depth'])
    for quantity,ind,reference in zip(['P_kN','Mx_kNm','My_kNm','min_strain'],actual,[point['P'],point['Mx'],point['My'],point['MinSteelStrain']]):
        error=ind-reference;tol=1e-8*max(1,abs(reference))
        assert abs(error)<tol,(name,quantity,ind,reference,error)
        records.append([name,quantity,reference,ind,error,tol])
        print('PASS QUADRATURE',name,quantity,'error=',error)

compare('circular_balanced',data['Circular'],data['CircularBalanced'])
compare('biaxial_fixed_state',data['Biaxial'],data['BiaxialFixed'])
compare('biaxial_exact_target',data['Biaxial'],data['BiaxialSolved'])
compare('manual_extreme_bar_balanced',data['Manual'],data['ManualBalanced'])
compare('manual_weighted_depth_diagnostic',data['Manual'],data['ManualMeanDepth'])

p=data['Biaxial'];target=426*4.4482216152605
def equations(x):
    force,mx,my,_=evaluate(p,x[0],math.exp(x[1]))
    return [(force-target)/target,(mx-1.6*my)/400]
answer=[135,math.log(310)]
for _ in range(25):
    f,g=equations(answer)
    if max(abs(f),abs(g))<1e-12:break
    step=1e-5
    f1,g1=equations([answer[0]+step,answer[1]]);f2,g2=equations([answer[0],answer[1]+step])
    a,b,cc,d=(f1-f)/step,(f2-f)/step,(g1-g)/step,(g2-g)/step;det=a*d-b*cc
    dx=(-d*f+b*g)/det;dy=(cc*f-a*g)/det
    answer=[answer[0]+dx,answer[1]+dy]
assert max(map(abs,equations(answer)))<1e-10
assert abs(answer[0]-data['BiaxialSolved']['Angle'])<1e-7
assert abs(math.exp(answer[1])-data['BiaxialSolved']['Depth'])<1e-6
print('PASS INDEPENDENT simultaneous root: theta=',answer[0],'depth=',math.exp(answer[1]))

# Inverse diagnostic, not a fitted material/input used by the engine.
p=data['Manual'];cb=1900*.003/(.003+300/200000)
c_source=bisect(lambda c:evaluate(p,90,c)[1]/evaluate(p,90,c)[0]*1000-545.084,1200,1266)
refstate=evaluate(p,90,c_source)
assert abs(refstate[3]+.0015)>1e-5
print('SOURCE_DIAGNOSTIC eb545.084 requires c=',c_source,'implied d=',c_source*1.5,'actual min strain=',refstate[3],'; physical cb=',cb)
with Path(sys.argv[2]).open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['State','Quantity','CSharp','IndependentQuadrature','Difference','Tolerance']);w.writerows(records)
print('PASS QUADRATURE total:',len(records)+2)
