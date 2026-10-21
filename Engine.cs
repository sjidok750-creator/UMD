using System;
using System.Collections.Generic;
using System.Linq;

namespace PMStudio
{
    public static class Engine
    {
        public static bool Finite(double v) { return !double.IsNaN(v)&&!double.IsInfinity(v); }
        static void Require(bool yes,string message) { if(!yes)throw new ArgumentException(message); }
        public static void Validate(Project p)
        {
            Require(p!=null,"프로젝트가 없습니다.");
            Require(p.Format=="PMStudio.Project"&&p.Version=="1.0","지원하지 않는 프로젝트 형식/버전입니다.");
            Require(p.Units=="mm-MPa-kN-kNm"&&p.Model=="RC-RectangularBlock-Nominal-v1","단위 또는 재료모델이 지원 범위를 벗어납니다.");
            Require(p.ReductionProfile=="Nominal"||p.ReductionProfile=="Reference-ACI31819-Tied"||p.ReductionProfile=="Reference-ACI31819-Spiral"||p.ReductionProfile=="Reference-KR2008-Tied","지원하지 않는 계수 비교 모드입니다.");
            Require(Finite(p.CircularRadius)&&p.CircularRadius>=0&&p.CircularRadius<=100000,"원형 적분 반경은 0 또는 양의 유한수입니다.");
            Require(p.BarDisplacement=="ExactDisk"||p.BarDisplacement=="CenterLumped","철근 공제 모델은 ExactDisk / CenterLumped입니다.");
            Require(!string.IsNullOrWhiteSpace(p.Name)&&p.Name.Length<=120,"프로젝트 이름은 1~120자입니다.");
            Material m=p.Material;Require(m!=null,"재료가 없습니다.");
            Require(Finite(m.Fc)&&m.Fc>=10&&m.Fc<=100,"Fc: 10~100 MPa 범위입니다.");
            Require(Finite(m.Fy)&&m.Fy>=100&&m.Fy<=1000,"Fy: 100~1000 MPa 범위입니다.");
            Require(Finite(m.Es)&&m.Es>=100000&&m.Es<=250000,"Es: 100000~250000 MPa 범위입니다.");
            Require(Finite(m.EpsCu)&&m.EpsCu>=0.001&&m.EpsCu<=0.01,"극한 압축변형률: 0.001~0.01 범위입니다.");
            Require(Finite(m.Alpha)&&m.Alpha>0&&m.Alpha<=1&&Finite(m.Beta)&&m.Beta>0&&m.Beta<=1,"Alpha/Beta는 0 초과, 1 이하입니다.");
            if(p.ReductionProfile=="Reference-ACI31819-Tied"||p.ReductionProfile=="Reference-ACI31819-Spiral")
            {
                double beta=Math.Max(0.65,Math.Min(0.85,0.85-0.05*(m.Fc/0.006894757293168-4000)/1000));
                Require(Math.Abs(m.EpsCu-0.003)<1e-9&&Math.Abs(m.Alpha-0.85)<1e-9&&Math.Abs(m.Beta-beta)<0.0001,"ACI 참고 계수 모드: εcu=0.003, α=0.85, β1은 입력 Fc의 ACI 값이어야 합니다.");
            }
            if(p.ReductionProfile=="Reference-KR2008-Tied")Require(Math.Abs(m.Fc-24)<1e-8&&Math.Abs(m.Fy-300)<1e-8&&Math.Abs(m.Es-200000)<1e-6&&Math.Abs(m.EpsCu-0.003)<1e-9&&Math.Abs(m.Alpha-0.85)<1e-9&&Math.Abs(m.Beta-0.85)<1e-9,"편람 2008 검산 모드는 예제의 Fc24 / Fy300 / Es200000 / εcu0.003 / αβ0.85를 사용합니다.");
            Require(p.AngleCount>=24&&p.AngleCount<=144&&p.AngleCount%4==0,"방향 수: 24~144, 4의 배수입니다.");
            Require(p.DepthCount>=24&&p.DepthCount<=180,"깊이 분할 수: 24~180 범위입니다.");
            Require(Finite(p.SliceP),"절단 축력은 유한한 수여야 합니다.");
            Require(p.Contours!=null&&p.Contours.Count>=1&&p.Contours.Count<=9,"외곽 1개와 개구부 최대 8개를 지원합니다.");
            Require(p.Contours.Count(c=>c!=null&&!c.Hole)==1,"콘크리트 외곽은 1개여야 합니다.");
            int nv=0;
            foreach(Contour c in p.Contours)
            {
                Require(c!=null&&c.Points!=null&&c.Points.Count>=3&&c.Points.Count<=512,"윤곽별 꼭짓점: 3~512개입니다.");nv+=c.Points.Count;
                foreach(Point2 v in c.Points)Require(v!=null&&Finite(v.X)&&Finite(v.Y)&&Math.Abs(v.X)<=100000&&Math.Abs(v.Y)<=100000,"좌표는 ±100000 mm 범위의 유한수여야 합니다.");
                Require(Geometry.Integral(c.Points).Area>=1,"윤곽 면적이 너무 작습니다.");
                for(int i=0;i<c.Points.Count;i++)
                {
                    Point2 a=c.Points[i],b=c.Points[(i+1)%c.Points.Count];Require(Geometry.Distance(a,b,b)>1e-5,"중복 꼭짓점이 있습니다. 마지막 점에 시작점을 반복하지 마세요.");
                    for(int j=i+1;j<c.Points.Count;j++)
                    {
                        if(j==i+1||(i==0&&j==c.Points.Count-1))continue;
                        Require(!Geometry.Intersects(a,b,c.Points[j],c.Points[(j+1)%c.Points.Count]),"자기교차/접촉 윤곽은 지원하지 않습니다.");
                    }
                }
            }
            Require(nv<=800,"전체 꼭짓점은 800개 이하입니다.");
            Contour outer=p.Contours.First(c=>!c.Hole);
            if(p.CircularRadius>0)
            {
                Require(p.Contours.Count==1&&outer.Points.Count>=32&&outer.Points.Count%4==0,"원형 적분은 개구부 없는 등분 원형 윤곽만 지원합니다.");
                double ox=outer.Points.Average(v=>v.X),oy=outer.Points.Average(v=>v.Y),step=2*Math.PI/outer.Points.Count;
                for(int i=0;i<outer.Points.Count;i++){Point2 v=outer.Points[i],next=outer.Points[(i+1)%outer.Points.Count];Require(Math.Abs(Math.Sqrt((v.X-ox)*(v.X-ox)+(v.Y-oy)*(v.Y-oy))-p.CircularRadius)<1e-5,"원형 적분 반경과 윤곽 좌표가 다릅니다.");double angle=Math.Atan2(next.Y-oy,next.X-ox)-Math.Atan2(v.Y-oy,v.X-ox);while(angle<0)angle+=2*Math.PI;Require(Math.Abs(angle-step)<1e-7||Math.Abs(angle-(2*Math.PI-step))<1e-7,"원형 윤곽 꼭짓점은 원주에 등분 배치되어야 합니다.");}
            }
            foreach(Contour h in p.Contours.Where(c=>c.Hole))Require(Geometry.Inside(h.Points[0],outer.Points),"개구부가 외곽 밖에 있습니다.");
            for(int i=0;i<p.Contours.Count;i++)for(int j=i+1;j<p.Contours.Count;j++)
            {
                Contour a=p.Contours[i],b=p.Contours[j];
                for(int k=0;k<a.Points.Count;k++)for(int l=0;l<b.Points.Count;l++)Require(!Geometry.Intersects(a.Points[k],a.Points[(k+1)%a.Points.Count],b.Points[l],b.Points[(l+1)%b.Points.Count]),"윤곽끼리 교차/접촉합니다.");
                if(a.Hole&&b.Hole)Require(!Geometry.Inside(a.Points[0],b.Points)&&!Geometry.Inside(b.Points[0],a.Points),"개구부가 중첩됩니다.");
            }
            Require(p.Bars!=null&&p.Bars.Count>=1&&p.Bars.Count<=300,"철근 수: 1~300개입니다.");
            for(int i=0;i<p.Bars.Count;i++)
            {
                Rebar b=p.Bars[i];Require(b!=null&&Finite(b.X)&&Finite(b.Y)&&Finite(b.Area)&&b.Area>0&&b.Area<=20000,"철근 좌표/면적이 잘못되었습니다.");
                Point2 q=new Point2(b.X,b.Y);Require(Geometry.Inside(q,outer.Points),"철근이 외곽 밖에 있습니다.");
                foreach(Contour c in p.Contours)
                {
                    if(c.Hole)Require(!Geometry.Inside(q,c.Points),"철근이 개구부 안에 있습니다.");
                    for(int k=0;k<c.Points.Count;k++)Require(Geometry.Distance(q,c.Points[k],c.Points[(k+1)%c.Points.Count])>=Math.Sqrt(b.Area/Math.PI)-1e-7,"철근 원이 콘크리트 경계를 침범합니다.");
                }
                for(int j=0;j<i;j++) { Rebar a=p.Bars[j];Require(Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y))>=Math.Sqrt(a.Area/Math.PI)+Math.Sqrt(b.Area/Math.PI)-1e-7,"철근 원이 겹칩니다."); }
            }
            RegionIntegral g=Geometry.Region(p,0,1,0,true);Require(g.Area>p.Bars.Sum(b=>b.Area),"콘크리트 순면적이 0 이하입니다.");
            Require(p.Loads!=null&&p.Loads.Count<=40,"하중조합은 최대 40개입니다.");
            HashSet<string> names=new HashSet<string>(StringComparer.Ordinal);
            foreach(Demand d in p.Loads) { Require(d!=null&&!string.IsNullOrWhiteSpace(d.Name)&&d.Name.Length<=120&&names.Add(d.Name),"하중조합 이름은 비어 있거나 중복될 수 없습니다.");Require(Finite(d.P)&&Finite(d.Mx)&&Finite(d.My)&&Math.Abs(d.P)<=1e10&&Math.Abs(d.Mx)<=1e10&&Math.Abs(d.My)<=1e10,"하중은 ±1e10 이내 유한수여야 합니다."); }
            int unique=p.Loads.Select(d=>d.P).Concat(new[]{p.SliceP}).Distinct().Count();
            Require((long)(unique+3)*p.AngleCount*(nv+p.Bars.Count)<=2000000,"한 번의 계산량이 너무 큽니다. 조합/방향/꼭짓점 수를 줄여 주세요.");
        }
        public static CapacityPoint Evaluate(Project p,double angle,double depth,bool details)
        { return new SectionSolver(p).Evaluate(angle,depth,details); }
        public static CapacityPoint AtP(Project p,double angle,double axial)
        { return new SectionSolver(p).AtP(angle,axial); }
        public static List<CapacityPoint> Slice(Project p,double axial,int angles)
        { return new SectionSolver(p).Slice(axial,angles); }
        static bool OnSegment(double x,double y,CapacityPoint a,CapacityPoint b)
        {
            double dx=b.Mx-a.Mx,dy=b.My-a.My,len=Math.Sqrt(dx*dx+dy*dy);
            if(len<1e-12)return Math.Sqrt((x-a.Mx)*(x-a.Mx)+(y-a.My)*(y-a.My))<1e-7;
            double t=((x-a.Mx)*dx+(y-a.My)*dy)/(len*len);return t>=-1e-9&&t<=1+1e-9&&Math.Abs((x-a.Mx)*dy-(y-a.My)*dx)/len<1e-7;
        }
        public static bool InsideSlice(List<CapacityPoint> s,double x,double y)
        {
            bool inside=false;for(int i=0,j=s.Count-1;i<s.Count;j=i++) { CapacityPoint a=s[i],b=s[j];if(OnSegment(x,y,a,b))return true;if((a.My>y)!=(b.My>y)&&x<(b.Mx-a.Mx)*(y-a.My)/(b.My-a.My)+a.Mx)inside=!inside;}return inside;
        }
        public static DemandResult Check(Demand d,List<CapacityPoint> slice)
        {
            DemandResult r=new DemandResult{Name=d.Name,P=d.P,Mx=d.Mx,My=d.My};
            if(slice.Count<3) { r.Status="OutsideNominal";r.Note="공칭 축력 범위 밖";return r; }
            bool inside=InsideSlice(slice,d.Mx,d.My);r.Status=inside?"InsideNominal":"OutsideNominal";
            double spread=slice.Max(q=>q.Mx)-slice.Min(q=>q.Mx)+slice.Max(q=>q.My)-slice.Min(q=>q.My);
            if(spread<1e-7){r.Note="축력 끝점의 모멘트 경계가 한 점으로 축소되어 사용률 미정의";return r;}
            for(int i=0;i<slice.Count;i++)for(int j=i+2;j<slice.Count;j++)
            {
                if(i==0&&j==slice.Count-1)continue;CapacityPoint a=slice[i],b=slice[(i+1)%slice.Count],c=slice[j],e=slice[(j+1)%slice.Count];
                if(Math.Abs(a.Mx-b.Mx)+Math.Abs(a.My-b.My)<1e-8||Math.Abs(c.Mx-e.Mx)+Math.Abs(c.My-e.My)<1e-8)continue;
                if(Geometry.Intersects(new Point2(a.Mx,a.My),new Point2(b.Mx,b.My),new Point2(c.Mx,c.My),new Point2(e.Mx,e.My))) { r.Status="Unresolved";r.Note="절단 경계가 교차/접촉하여 포함 여부를 확정할 수 없음";return r; }
            }
            double radius=Math.Sqrt(d.Mx*d.Mx+d.My*d.My);
            if(radius<1e-9) { r.Note="영모멘트 점의 공칭영역 포함 여부; 사용률 미정의";return r; }
            if(!InsideSlice(slice,0,0)) { r.Note="원점이 절단영역 밖에 있어 방사형 사용률 미정의";return r; }
            double ux=d.Mx/radius,uy=d.My/radius,best=double.PositiveInfinity;
            for(int i=0;i<slice.Count;i++)
            {
                CapacityPoint a=slice[i],b=slice[(i+1)%slice.Count];double vx=b.Mx-a.Mx,vy=b.My-a.My,den=ux*vy-uy*vx;
                if(Math.Abs(den)<1e-12)continue;
                double distance=(a.Mx*vy-a.My*vx)/den,k=(a.Mx*uy-a.My*ux)/den;
                if(distance>1e-8&&k>=-1e-9&&k<=1+1e-9)best=Math.Min(best,distance);
            }
            if(!Finite(best)) { r.Status="Unresolved";r.Note="방사선과 경계의 교점을 찾지 못함";return r; }
            r.Utilization=radius/best;r.CapacityMx=best*ux;r.CapacityMy=best*uy;r.Note="동일 축력에서 모멘트 방향 고정; 설계 안전율이 아님";return r;
        }
        public static Analysis Analyze(Project p)
        {
            Validate(p);SectionSolver solver=new SectionSolver(p);Analysis r=new Analysis();RegionIntegral a=Geometry.Region(p,0,1,0,true);
            r.GrossArea=a.Area;r.Cx=a.Qx/a.Area;r.Cy=a.Qy/a.Area;r.SteelArea=p.Bars.Sum(b=>b.Area);r.PMin=solver.PMin;r.PMax=solver.PMax;r.SliceP=p.SliceP;
            for(int i=0;i<p.AngleCount;i++)
            {
                double angle=i*360.0/p.AngleCount,t=angle*Math.PI/180,nx=Math.Cos(t),ny=Math.Sin(t);
                double[] qs=p.Contours.Where(c=>!c.Hole).SelectMany(c=>c.Points).Select(v=>nx*v.X+ny*v.Y).ToArray();double h=qs.Max()-qs.Min();
                List<CapacityPoint> curve=new List<CapacityPoint>();curve.Add(solver.Evaluate(angle,0,false));
                for(int j=0;j<=p.DepthCount;j++) { double c=h*Math.Pow(10,-4+8.0*j/p.DepthCount);curve.Add(solver.Evaluate(angle,c,false)); }
                curve.Add(solver.Evaluate(angle,double.PositiveInfinity,false));r.Curves.Add(curve);
            }
            Dictionary<double,List<CapacityPoint>> cuts=new Dictionary<double,List<CapacityPoint>>();
            cuts[p.SliceP]=solver.Slice(p.SliceP,p.AngleCount*2);r.Slice=cuts[p.SliceP];
            foreach(Demand d in p.Loads)
            {
                if(!cuts.ContainsKey(d.P))cuts[d.P]=solver.Slice(d.P,p.AngleCount*2);
                DemandResult fine=Check(d,cuts[d.P]),coarse=Check(d,solver.Slice(d.P,p.AngleCount));
                if(fine.Utilization.HasValue&&coarse.Utilization.HasValue)
                {
                    double change=Math.Abs(fine.Utilization.Value-coarse.Utilization.Value)/Math.Max(1e-9,fine.Utilization.Value);r.AngularChange=Math.Max(r.AngularChange,change);
                    if(change>0.01||fine.Status!=coarse.Status) { fine.Status="Unresolved";fine.Note="방향 세분화에 민감합니다. 방향 수를 늘려 다시 계산하세요."; }
                }
                else if(fine.Status!=coarse.Status) { fine.Status="Unresolved";fine.Note="방향 세분화에서 포함 판정이 바뀜"; }
                r.Demands.Add(fine);
            }
            List<double> axial=new List<double>();for(int i=0;i<=50;i++)axial.Add(r.PMin+(r.PMax-r.PMin)*i/50);axial.Add(0);axial.Add(p.SliceP);axial.AddRange(p.Loads.Select(d=>d.P));
            var levels=axial.Where(v=>v>=r.PMin&&v<=r.PMax).Distinct().OrderBy(v=>v);
            List<CapacityPoint> xp=new List<CapacityPoint>(),xn=new List<CapacityPoint>(),yp=new List<CapacityPoint>(),yn=new List<CapacityPoint>();
            foreach(double v in levels)
            {
                List<CapacityPoint> cut=cuts.ContainsKey(v)?cuts[v]:solver.Slice(v,p.AngleCount);
                if(cut.Count==p.AngleCount)r.DisplayRings.Add(cut);
                else if(cut.Count==2*p.AngleCount){List<CapacityPoint> ring=new List<CapacityPoint>(p.AngleCount);for(int k=0;k<cut.Count;k+=2)ring.Add(cut[k]);r.DisplayRings.Add(ring);}
                List<CapacityPoint> xx=AxisIntersections(solver,v,cut,true),yy=AxisIntersections(solver,v,cut,false);
                AddAxis(xx,true,xp,xn,r.PMxPaths);AddAxis(yy,false,yp,yn,r.PMyPaths);
            }
            if(xp.Count>0)r.PMxPaths.Add(xp);if(xn.Count>0)r.PMxPaths.Add(xn);if(yp.Count>0)r.PMyPaths.Add(yp);if(yn.Count>0)r.PMyPaths.Add(yn);
            r.Limits.YieldStrain=p.Material.Fy/p.Material.Es;r.Limits.BalancedMx=BalancedAxis(solver,true,p.AngleCount);r.Limits.BalancedMy=BalancedAxis(solver,false,p.AngleCount);
            List<CapacityPoint> zero=solver.Slice(0,p.AngleCount);r.Limits.PureBendingMx=AxisIntersections(solver,0,zero,true);r.Limits.PureBendingMy=AxisIntersections(solver,0,zero,false);
            r.Notes.Add("일반 등가 직사각형 응력블록의 공칭 단면내력. KDS/ACI/AASHTO 기준 적용 인증 없음.");
            r.Notes.Add("콘크리트 인장 무시; 철근 완전부착·탄성완전소성; 파단·좌굴·전단·장주·2차효과·프리스트레스 제외.");
            r.Notes.Add("철근 응력은 중심 변형률로 평가; 콘크리트 공제 모델: "+p.BarDisplacement+" (ExactDisk: 원호 적분 / CenterLumped: 중심 기준 전면적 공제).");
            r.Notes.Add("곡면 표시용 깊이는 로그 분할; 하중 검토용 축력 절단은 별도 평형 반복계산. 방향 분할/2배 분할 비교.");
            r.Notes.Add("P–Mx / P–My는 반대축 모멘트 0을 방향 반복계산으로 만족하는 경로. 축력 51개와 입력 조합 축력 등에서 산정.");
            r.Notes.Add("균형점은 콘크리트 εcu와 최외단 인장철근 εt=Fy/Es의 동시 도달점. 항복 전/후 표시는 기준별 압축·인장지배 판정과 다름.");
            DirectionalSolver.Populate(p,r,solver);
            if(p.ReductionProfile!="Nominal"){r.Strength="Nominal plus "+p.ReductionProfile+"; reference calculation only, no full member code certification";r.Notes.Add(Reduction.Label(p.ReductionProfile)+" · 현재 KDS 전체 설계검토가 아닌 해당 예제의 계수 검산.");}
            if(p.ReductionProfile=="Reference-KR2008-Tied")r.Notes.Add("편람 2008 PDF846~847: 단면적/철근량/최대 축력은 일치. 평형편심 545.084 mm와 실제 최외단 철근 기준 계산은 불일치하여 원 예제 재현 판정 보류. 편람 표의 일부 편심값도 Mu/Pu와 일치하지 않음.");
            r.Notes.Add("보고서 Pmax / |Mx|max / |My|max는 동일 조합의 P·Mx·My를 유지하고, 합성 모멘트 방향 고정 절단을 적응 세분화함. 곡선오차 지표는 중점 현 오차이며 전역오차 보증이 아님.");
            return r;
        }
        static List<CapacityPoint> BalancedAxis(SectionSolver solver,bool mxAxis,int count)
        {
            List<CapacityPoint> all=new List<CapacityPoint>();Func<CapacityPoint,double> cross=q=>mxAxis?q.My:q.Mx;
            for(int i=0;i<count;i++)
            {
                double l=i*360.0/count,h=(i+1)*360.0/count;CapacityPoint a=solver.Balanced(l),b=solver.Balanced(h);double fa=cross(a),fb=cross(b);
                if(Math.Abs(fa)<1e-7){all.Add(a);continue;}if(fa*fb>=0)continue;CapacityPoint q=null;
                for(int k=0;k<50;k++){double t=(l+h)/2;q=solver.Balanced(t);double f=cross(q);if(Math.Abs(f)<1e-7)break;if(f*fa>0){l=t;fa=f;}else h=t;}
                if(q!=null&&Math.Abs(cross(q))<1e-5)all.Add(q);
            }
            List<CapacityPoint> result=new List<CapacityPoint>();if(all.Count==0)return result;result.Add(all.OrderBy(q=>mxAxis?q.Mx:q.My).First());CapacityPoint high=all.OrderByDescending(q=>mxAxis?q.Mx:q.My).First();if(!ReferenceEquals(high,result[0]))result.Add(high);return result;
        }
        public static string StrainState(CapacityPoint q,Material m)
        {
            if(q.Depth==0)return "균일 인장 끝점";if(q.Depth<0)return "균일 압축 끝점";
            double et=Math.Max(0,-q.MinSteelStrain),ey=m.Fy/m.Es;if(Math.Abs(et-ey)<=Math.Max(1e-8,ey*1e-5))return "균형 변형률";return et<ey?"인장철근 항복 전":"인장철근 항복 후";
        }
        static void AddAxis(List<CapacityPoint> pts,bool x,List<CapacityPoint> pos,List<CapacityPoint> neg,List<List<CapacityPoint>> paths)
        {
            if(pts.Count==0){if(pos.Count>0){paths.Add(new List<CapacityPoint>(pos));pos.Clear();}if(neg.Count>0){paths.Add(new List<CapacityPoint>(neg));neg.Clear();}return;}
            pos.Add(pts.OrderByDescending(q=>x?q.Mx:q.My).First());neg.Add(pts.OrderBy(q=>x?q.Mx:q.My).First());
        }
        public static List<CapacityPoint> AxisIntersections(Project p,double axial,List<CapacityPoint> cut,bool mxAxis)
        { return AxisIntersections(new SectionSolver(p),axial,cut,mxAxis); }
        static List<CapacityPoint> AxisIntersections(SectionSolver solver,double axial,List<CapacityPoint> cut,bool mxAxis)
        {
            List<CapacityPoint> pts=new List<CapacityPoint>();Func<CapacityPoint,double> cross=q=>mxAxis?q.My:q.Mx;
            for(int i=0;i<cut.Count;i++)
            {
                CapacityPoint a=cut[i],b=cut[(i+1)%cut.Count];double fa=cross(a),fb=cross(b);
                if(Math.Abs(fa)<1e-7){pts.Add(a);continue;}if(fa*fb>=0)continue;
                double l=a.Angle,h=i+1==cut.Count?360:b.Angle;CapacityPoint q=null;
                for(int j=0;j<45;j++){double t=(l+h)/2;q=solver.AtP(t,axial);double f=cross(q);if(Math.Abs(f)<1e-7)break;if(f*fa>0){l=t;fa=f;}else h=t;}
                if(q!=null&&Math.Abs(cross(q))<1e-5)pts.Add(q);
            }return pts;
        }
    }
}
