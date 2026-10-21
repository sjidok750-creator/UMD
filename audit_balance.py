"""Audit input and definition changes without fitting the calculation to a target."""
import math,json,csv,sys
from pathlib import Path
data=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'));p=data['Manual']
def ev(c,area=506.7,beta=.85,phase=90):
    r=1000;level=r-beta*c;h=r*r-level*level
    ac=r*r*math.acos(level/r)-level*math.sqrt(h);qc=2/3*h**1.5
    pp=ac*20.4;mm=qc*20.4;et=0
    for n,radius in [(50,900),(45,800)]:
        for i in range(n):
            y=radius*math.sin(math.radians(phase+i*360/n));e=.003*(1-(1000-y)/c)
            f=(min(300,max(-300,200000*e))-(20.4 if y>=level else 0))*area
            pp+=f;mm+=f*y;et=min(et,e)
    return pp/1000,mm/1e6,mm/pp,et
mean_d=2000-(50*100+45*200)/95
cases=[('Actual_extreme_bar_d1900',1900*2/3,506.7),('Printed_weighted_depth_d1852.632',mean_d*2/3,506.7),('Printed_figure_depth_d1852.526',1852.526*2/3,506.7),('Wrong_area_pi25_squared_not_used_in_manual',1900*2/3,math.pi*25**2/4)]
rows=[]
for label,c,area in cases:
    pp,mm,eb,et=ev(c,area);rows.append([label,c,area,pp,mm,eb,et,eb/545.084-1])
    print(label,'c=',c,'As_each=',area,'eb=',eb,'min_steel_strain=',et,'difference_from_printed=',eb/545.084-1)
assert abs(rows[0][5]-513.0873225509879)<1e-8
assert abs(rows[0][6]+.0015)<1e-12
assert abs(rows[1][6]+.0015)>1e-5
with Path(sys.argv[2]).open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.writer(f);writer.writerow(['Case','c_mm','Area_each_mm2','P_kN','M_kNm','e_mm','MinSteelStrain','RelativeToPublished_e545.084']);writer.writerows(rows)
print('D25 area shortfall relative to nominal percent=',100*((math.pi*25**2/4)/506.7-1))
print('Difference attribution, NOT proof of the original program algorithm:')
print('Depth-definition contribution in mm=',rows[1][5]-rows[0][5])
print('Residual at the printed weighted depth in mm=',545.084-rows[1][5])
