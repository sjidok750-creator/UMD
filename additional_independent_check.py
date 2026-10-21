"""Read exported inputs/states; do not import or call PMStudio.
Alternative integration: vertical polygon intervals + piecewise Gaussian quadrature.
Steel disk displacement uses numerical theta quadrature, not C# circular-segment expressions.
Usage: python additional_independent_check.py states.json comparison.csv
"""
import csv,json,math,sys
from pathlib import Path

def smooth_integral(fn,a,b,tol=1e-12):
    def simp(lo,hi):
        return (hi-lo)*(fn(lo)+4*fn((lo+hi)/2)+fn(hi))/6
    def go(lo,hi,whole,budget,depth):
        mid=(lo+hi)/2;left=simp(lo,mid);right=simp(mid,hi)
        if depth==0 or abs(left+right-whole)<15*budget:
            return left+right+(left+right-whole)/15
        return go(lo,mid,left,budget/2,depth-1)+go(mid,hi,right,budget/2,depth-1)
    return go(a,b,simp(a,b),tol,18)

def strip_region(contours,nx=0,ny=1,level=None):
    """Integrate each polygon's vertical intervals. No clipping/polygon moment formulas."""
    totals=[0.,0.,0.]
    for shape in contours:
        vertices=shape['Points'];edges=list(zip(vertices,vertices[1:]+vertices[:1]))
        breaks={v['X'] for v in vertices}
        if level is not None:
            for a,b in edges:
                fa=nx*a['X']+ny*a['Y']-level;fb=nx*b['X']+ny*b['Y']-level
                if fa*fb<0:
                    t=fa/(fa-fb);breaks.add(a['X']+t*(b['X']-a['X']))
        def section(x):
            crossings=[]
            for a,b in edges:
                if min(a['X'],b['X'])<x<max(a['X'],b['X']):
                    crossings.append(a['Y']+(x-a['X'])*(b['Y']-a['Y'])/(b['X']-a['X']))
            crossings.sort();area=qy=0.
            for low,high in zip(crossings[::2],crossings[1::2]):
                if level is not None:
                    if abs(ny)<1e-12:
                        if nx*x<level:continue
                    elif ny>0:low=max(low,(level-nx*x)/ny)
                    else:high=min(high,(level-nx*x)/ny)
                if high>low:
                    area+=high-low;qy+=(high*high-low*low)/2
            return area,x*area,qy
        acc=[0.,0.,0.];xs=sorted(breaks)
        for low,high in zip(xs,xs[1:]):
            if high-low<1e-9:continue
            mid=(low+high)/2;half=(high-low)/2
            for node,weight in [(-math.sqrt(3/5),5/9),(0,8/9),(math.sqrt(3/5),5/9)]:
                v=section(mid+half*node)
                for i in range(3):acc[i]+=half*weight*v[i]
        sign=-1 if shape['Hole'] else 1
        for i in range(3):totals[i]+=sign*acc[i]
    return totals

gross_cache={}
def evaluate(p,q):
    m=p['Material'];angle=math.radians(q['Angle']);nx,ny=math.cos(angle),math.sin(angle)
    key=p['Name'];radius=p.get('CircularRadius',0)
    if key not in gross_cache:
        if radius:
            v=p['Contours'][0]['Points'];cx=sum(a['X'] for a in v)/len(v);cy=sum(a['Y'] for a in v)/len(v)
            gross_cache[key]=(math.pi*radius**2,cx,cy)
        else:
            ga,gqx,gqy=strip_region(p['Contours']);gross_cache[key]=(ga,gqx/ga,gqy/ga)
    ga,cx,cy=gross_cache[key]
    support=(nx*cx+ny*cy+radius) if radius else max(nx*v['X']+ny*v['Y'] for c in p['Contours'] if not c['Hole'] for v in c['Points'])
    depth=q['Depth'];full=depth<0;tension=depth==0;level=None if full else support-m['Beta']*depth
    if tension:a=qx=qy=0.
    elif full:a,qx,qy=ga,ga*cx,ga*cy
    elif radius:
        relative=max(-1,min(1,(level-nx*cx-ny*cy)/radius));theta=math.acos(relative)
        a=2*radius**2*smooth_integral(lambda t:math.sin(t)**2,0,theta)
        first=2*radius**3*smooth_integral(lambda t:math.cos(t)*math.sin(t)**2,0,theta)
        qx=a*cx+nx*first;qy=a*cy+ny*first
    else:a,qx,qy=strip_region(p['Contours'],nx,ny,level)
    # Integrate the area of each bar lying in the compression region separately.
    if not tension:
        for b in p['Bars']:
            ba=b['Area'];first=0.;r=math.sqrt(ba/math.pi)
            if not full:
                distance=level-nx*b['X']-ny*b['Y']
                if p['BarDisplacement']=='CenterLumped':
                    if distance>0:continue
                elif distance>=r:continue
                elif distance>-r:
                    theta=math.acos(max(-1,min(1,distance/r)))
                    ba=2*r*r*smooth_integral(lambda t:math.sin(t)**2,0,theta)
                    first=2*r**3*smooth_integral(lambda t:math.cos(t)*math.sin(t)**2,0,theta)
            a-=ba;qx-=ba*b['X']+nx*first;qy-=ba*b['Y']+ny*first
    stress=m['Alpha']*m['Fc'];force=stress*a;mx=stress*(qy-cy*a);my=-stress*(qx-cx*a);strains=[]
    for b in p['Bars']:
        e=(-m['Fy']/m['Es']) if tension else m['EpsCu'] if full else m['EpsCu']*(1-(support-nx*b['X']-ny*b['Y'])/depth)
        strains.append(e);s=max(-m['Fy'],min(m['Fy'],m['Es']*e));f=s*b['Area'];force+=f;mx+=f*(b['Y']-cy);my-=f*(b['X']-cx)
    return force/1000,mx/1e6,my/1e6,min(strains)

records=[];maximum=[0.,0.,0.,0.];states=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
for item in states:
    q=item['Point'];independent=evaluate(item['Project'],q)
    for i,(quantity,actual,expected) in enumerate(zip(['P_kN','Mx_kNm','My_kNm','min_strain'],independent,[q['P'],q['Mx'],q['My'],q['MinSteelStrain']])):
        difference=actual-expected;tolerance=1e-8*max(1,abs(expected))
        assert abs(difference)<=tolerance,(item['Name'],quantity,actual,expected,difference,tolerance)
        records.append([item['Name'],quantity,expected,actual,difference,tolerance]);maximum[i]=max(maximum[i],abs(difference))
with Path(sys.argv[2]).open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['State','Quantity','CSharp','IndependentStripIntegration','Difference','Tolerance']);w.writerows(records)
print('PASS STRIP states:',len(states),'values:',len(records))
print('MAX_ABSOLUTE_DIFFERENCE P_kN Mx_kNm My_kNm min_strain:',*maximum)
print('PASS STRIP total:',len(records))
