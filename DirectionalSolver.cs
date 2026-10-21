using System;
using System.Collections.Generic;
using System.Linq;

namespace PMStudio
{
    // A vertical cut of the true biaxial surface: the MOMENT direction is held fixed.
    // The compression-face normal is solved independently at every axial level.
    public static class DirectionalSolver
    {
        public static CapacityPoint AtTensileStrain(Project p,double momentAngle,double tensileStrain)
        {Engine.Validate(p);if(!Engine.Finite(momentAngle)||!Engine.Finite(tensileStrain)||tensileStrain<0)throw new ArgumentException("방향각과 인장변형률을 확인하세요.");return Balanced(new SectionSolver(p),momentAngle,p.AngleCount,tensileStrain);}
        public static CapacityPoint AtP(Project p,double axial,double momentAngle)
        { Engine.Validate(p);if(!Engine.Finite(axial)||!Engine.Finite(momentAngle))throw new ArgumentException("축력과 방향각은 유한한 수여야 합니다.");return At(new SectionSolver(p),axial,momentAngle,p.AngleCount); }
        static double Projection(CapacityPoint q,double ux,double uy){return q.Mx*ux+q.My*uy;}
        static double Cross(CapacityPoint q,double ux,double uy){return q.My*ux-q.Mx*uy;}
        internal static CapacityPoint At(SectionSolver solver,double axial,double angle,int count)
        {
            double t=angle*Math.PI/180,ux=Math.Cos(t),uy=Math.Sin(t);
            CapacityPoint best=null;double radius=-1;
            CapacityPoint a=solver.AtP(0,axial);if(a==null)return null;
            for(int i=0;i<count;i++)
            {
                double l=i*360.0/count,h=(i+1)*360.0/count;CapacityPoint b=solver.AtP(h,axial);
                double fa=Cross(a,ux,uy),fb=Cross(b,ux,uy);CapacityPoint q=null;
                if(Math.Abs(fa)<1e-7)q=a;
                else if(fa*fb<0)
                {
                    for(int k=0;k<42;k++)
                    {
                        double mid=(l+h)/2;q=solver.AtP(mid,axial);double f=Cross(q,ux,uy);
                        if(Math.Abs(f)<1e-7)break;if(f*fa>0){l=mid;fa=f;}else h=mid;
                    }
                }
                if(q!=null&&Math.Abs(Cross(q,ux,uy))<1e-5)
                {
                    double m=Projection(q,ux,uy);if(m>=-1e-7&&m>radius){best=q;radius=m;}
                }
                a=b;
            }
            return best;
        }
        static CapacityPoint Balanced(SectionSolver solver,double angle,int count,double tensile)
        {
            double t=angle*Math.PI/180,ux=Math.Cos(t),uy=Math.Sin(t);CapacityPoint best=null;double radius=-1;
            for(int i=0;i<count;i++)
            {
                double l=i*360.0/count,h=(i+1)*360.0/count;CapacityPoint a=solver.AtTensileStrain(l,tensile),b=solver.AtTensileStrain(h,tensile),q=null;double fa=Cross(a,ux,uy),fb=Cross(b,ux,uy);
                if(Math.Abs(fa)<1e-7)q=a;else if(fa*fb<0)
                {
                    for(int k=0;k<45;k++){double mid=(l+h)/2;q=solver.AtTensileStrain(mid,tensile);double f=Cross(q,ux,uy);if(Math.Abs(f)<1e-7)break;if(f*fa>0){l=mid;fa=f;}else h=mid;}
                }
                if(q!=null&&Math.Abs(Cross(q,ux,uy))<1e-5&&Projection(q,ux,uy)>radius&&Projection(q,ux,uy)>=0){best=q;radius=Projection(q,ux,uy);}
            }
            return best;
        }
        sealed class Builder
        {
            readonly SectionSolver solver;readonly double angle,pscale,mscale;readonly int count;
            readonly Project project;
            readonly double reductionThreshold;
            readonly SortedDictionary<double,CapacityPoint> points=new SortedDictionary<double,CapacityPoint>();
            public bool Limited;public double Error;
            public Builder(SectionSolver s,double a,int n,double m,Project p,double threshold){solver=s;angle=a;count=n;pscale=s.PMax-s.PMin;mscale=Math.Max(1,m);project=p;reductionThreshold=threshold;}
            public CapacityPoint Get(double p){CapacityPoint q;if(!points.TryGetValue(p,out q)){q=At(solver,p,angle,count);points.Add(p,q);}return q;}
            public void Refine(double pa,double pb,int depth)
            {
                CapacityPoint a=Get(pa),b=Get(pb);double pm=(pa+pb)/2;CapacityPoint m=Get(pm);
                if(a==null||b==null||m==null)
                {
                    // Keep missing equilibria as actual gaps. Never join across a failed interval.
                    if(depth<5&&pb-pa>pscale/8192){if(a!=null||m!=null)Refine(pa,pm,depth+1);if(b!=null||m!=null)Refine(pm,pb,depth+1);}return;
                }
                double am=Math.Sqrt(a.Mx*a.Mx+a.My*a.My),bm=Math.Sqrt(b.Mx*b.Mx+b.My*b.My),mm=Math.Sqrt(m.Mx*m.Mx+m.My*m.My);
                // Distance from midpoint to the segment in normalized M/P coordinates.
                double dx=(bm-am)/mscale,dy=(b.P-a.P)/pscale,ex=(mm-am)/mscale,ey=(m.P-a.P)/pscale;
                double error=Math.Abs(dx*ey-dy*ex)/Math.Max(1e-15,Math.Sqrt(dx*dx+dy*dy));
                if(project.ReductionProfile!="Nominal")
                {
                    double af=Reduction.Factor(a,project,reductionThreshold),bf=Reduction.Factor(b,project,reductionThreshold),mf=Reduction.Factor(m,project,reductionThreshold);
                    dx=(bm*bf-am*af)/mscale;dy=(b.P*bf-a.P*af)/pscale;ex=(mm*mf-am*af)/mscale;ey=(m.P*mf-a.P*af)/pscale;
                    error=Math.Max(error,Math.Abs(dx*ey-dy*ex)/Math.Max(1e-15,Math.Sqrt(dx*dx+dy*dy)));
                }
                if(error>0.00025&&depth<7){Refine(pa,pm,depth+1);Refine(pm,pb,depth+1);}
                else{Error=Math.Max(Error,error);if(error>0.00025)Limited=true;}
            }
            public List<List<CapacityPoint>> Paths()
            {
                List<List<CapacityPoint>> paths=new List<List<CapacityPoint>>();List<CapacityPoint> path=new List<CapacityPoint>();
                foreach(var kv in points){if(kv.Value!=null)path.Add(kv.Value);else if(path.Count>0){paths.Add(path);path=new List<CapacityPoint>();}}
                if(path.Count>0)paths.Add(path);return paths;
            }
        }
        internal static DirectionalDiagram Build(Project p,Analysis r,SectionSolver solver,Demand d,string title,double fallbackAngle)
        {
            double magnitude=Math.Sqrt(d.Mx*d.Mx+d.My*d.My),angle=magnitude>1e-9?Math.Atan2(d.My,d.Mx)*180/Math.PI:fallbackAngle;if(angle<0)angle+=360;
            DirectionalDiagram report=new DirectionalDiagram{Title=title,Load=new Demand{Name=d.Name,P=d.P,Mx=d.Mx,My=d.My},MomentAngle=angle,HasMoment=magnitude>1e-9};
            report.Balanced=Balanced(solver,angle,p.AngleCount,p.Material.Fy/p.Material.Es);report.PureBending=At(solver,0,angle,p.AngleCount);report.AtDemand=At(solver,d.P,angle,p.AngleCount);
            report.Check=r.Demands.FirstOrDefault(x=>x.Name==d.Name);
            double maxMoment=new[]{report.Balanced,report.PureBending,report.AtDemand}.Where(q=>q!=null).Select(q=>Math.Sqrt(q.Mx*q.Mx+q.My*q.My)).DefaultIfEmpty(1).Max();double threshold=Math.Min(0.1*p.Material.Fc*r.GrossArea/1000,Math.Max(0,report.Balanced==null?0:report.Balanced.P*0.7));Builder builder=new Builder(solver,angle,p.AngleCount,maxMoment,p,threshold);
            List<double> levels=new List<double>();for(int i=0;i<=64;i++)levels.Add(solver.PMin+(solver.PMax-solver.PMin)*i/64);
            foreach(CapacityPoint q in new[]{report.Balanced,report.PureBending,report.AtDemand})if(q!=null)levels.Add(q.P);
            foreach(double strain in new[]{0.0,p.Material.Fy/p.Material.Es*0.5,p.Material.Fy/p.Material.Es+0.003}){CapacityPoint q=Balanced(solver,angle,p.AngleCount,strain);if(q!=null)levels.Add(q.P);}
            double[] ps=levels.Distinct().OrderBy(x=>x).ToArray();for(int i=0;i<ps.Length-1;i++)builder.Refine(ps[i],ps[i+1],0);
            report.Paths=builder.Paths();report.SolvedPoints=report.Paths.Sum(x=>x.Count);report.MaxChordError=builder.Error;report.ResolutionLimited=builder.Limited;
            report.Note="ψ = atan2(My, Mx). 각 P에서 모멘트 방향 평형을 반복계산. 면 채움·보간 스플라인 없음.";
            if(!report.HasMoment)report.Note="영모멘트 조합: ψ가 정의되지 않아 "+angle.ToString("0.##")+"° 참고 절단면을 표시. 이 방향으로 하중 검토하지 않음.";
            if(p.ReductionProfile!="Nominal")Reduction.Populate(p,r,solver,report);
            return report;
        }
        internal static void Populate(Project p,Analysis r,SectionSolver solver)
        {
            if(p.Loads.Count==0)return;
            Demand[] cases={p.Loads.OrderByDescending(d=>d.P).First(),p.Loads.OrderByDescending(d=>Math.Abs(d.Mx)).First(),p.Loads.OrderByDescending(d=>Math.Abs(d.My)).First()};
            string[] titles={"(b) P 최대 조합","(c) |Mx| 최대 조합","(d) |My| 최대 조합"};
            Dictionary<string,DirectionalDiagram> solved=new Dictionary<string,DirectionalDiagram>();
            for(int i=0;i<cases.Length;i++)
            {
                Demand d=cases[i];double mag=Math.Sqrt(d.Mx*d.Mx+d.My*d.My),angle=mag>1e-9?Math.Atan2(d.My,d.Mx)*180/Math.PI:i==2?90:0;if(angle<0)angle+=360;
                string key=Files.N(angle)+"|"+Files.N(d.P);DirectionalDiagram baseDiagram,diagram;
                if(solved.TryGetValue(key,out baseDiagram))
                {
                    diagram=new DirectionalDiagram{Title=titles[i],Load=new Demand{Name=d.Name,P=d.P,Mx=d.Mx,My=d.My},MomentAngle=angle,HasMoment=mag>1e-9,Paths=baseDiagram.Paths,Balanced=baseDiagram.Balanced,PureBending=baseDiagram.PureBending,AtDemand=baseDiagram.AtDemand,Check=r.Demands.FirstOrDefault(q=>q.Name==d.Name),SolvedPoints=baseDiagram.SolvedPoints,MaxChordError=baseDiagram.MaxChordError,ResolutionLimited=baseDiagram.ResolutionLimited,Note=baseDiagram.Note,ReductionProfile=baseDiagram.ReductionProfile,ReducedAxialCap=baseDiagram.ReducedAxialCap,ReducedPaths=baseDiagram.ReducedPaths,ReducedAtDemand=baseDiagram.ReducedAtDemand,ReducedCheck=baseDiagram.ReducedCheck,ReducedIntersectionCount=baseDiagram.ReducedIntersectionCount};
                    if(p.ReductionProfile!="Nominal"&&d.Name!=baseDiagram.Load.Name)Reduction.Recheck(p,solver,diagram);
                }
                else{diagram=Build(p,r,solver,d,titles[i],i==2?90:0);solved.Add(key,diagram);}
                r.ReportDiagrams.Add(diagram);
            }
        }
    }
}
