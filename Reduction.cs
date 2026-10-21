using System;
using System.Collections.Generic;

namespace PMStudio
{
    // Explicit verification profile only; not a KDS implementation or member design certification.
    public static class Reduction
    {
        public static double Factor(CapacityPoint q,Material m)
        {
            if(q.Depth==0)return 0.9;
            double strain=Math.Max(0,-q.MinSteelStrain),yield=m.Fy/m.Es;
            return 0.65+0.25*Math.Max(0,Math.Min(1,(strain-yield)/0.003));
        }
        public static StrengthPoint Apply(CapacityPoint q,Material m)
        {double f=Factor(q,m);return new StrengthPoint{P=f*q.P,Mx=f*q.Mx,My=f*q.My,Phi=f,Nominal=q};}
        public static string Label(string profile){return profile=="Reference-ACI31819-Spiral"?"ACI 318-19 나선철근 계수 검산":profile=="Reference-KR2008-Tied"?"도로설계편람 2008 계수 검산":profile=="Reference-ACI31819-Tied"?"ACI 318-19 계수 참고 비교":"공칭 단면내력";}
        public static double CompressionFactor(Project p){return p.ReductionProfile=="Reference-KR2008-Tied"?0.7:p.ReductionProfile=="Reference-ACI31819-Spiral"?0.75:0.65;}
        public static double CapFactor(Project p){return p.ReductionProfile=="Reference-ACI31819-Spiral"?0.85:0.8;}
        public static double TensionFactor(Project p){return p.ReductionProfile=="Reference-KR2008-Tied"?0.85:0.9;}
        public static double Factor(CapacityPoint q,Project p,double threshold)
        {
            if(p.ReductionProfile=="Reference-ACI31819-Spiral")return q.Depth==0?0.9:0.75+0.15*Math.Max(0,Math.Min(1,(Math.Max(0,-q.MinSteelStrain)-p.Material.Fy/p.Material.Es)/0.003));
            if(p.ReductionProfile!="Reference-KR2008-Tied")return Factor(q,p.Material);
            if(q.P<=0)return 0.85;
            if(threshold<=0||0.7*q.P>=threshold)return 0.7;
            // phi = .85 - .15*(phi*Pn/threshold), solved implicitly for phi.
            return 0.85/(1+0.15*q.P/threshold);
        }
        static double Threshold(Project p,DirectionalDiagram d){return Math.Min(0.1*p.Material.Fc*Geometry.Region(p,0,1,0,true).Area/1000,Math.Max(0,d.Balanced==null?0:d.Balanced.P*0.7));}
        public static StrengthPoint Apply(CapacityPoint q,Project p,double threshold)
        {double f=Factor(q,p,threshold);return new StrengthPoint{P=f*q.P,Mx=f*q.Mx,My=f*q.My,Phi=f,Nominal=q};}
        static double M(StrengthPoint q){return Math.Sqrt(q.Mx*q.Mx+q.My*q.My);}
        static StrengthPoint Solve(Project p,SectionSolver solver,double psi,CapacityPoint a,CapacityPoint b,double target,double threshold)
        {
            double lo=a.P,hi=b.P,fl=Apply(a,p,threshold).P-target;StrengthPoint q=null;
            for(int i=0;i<55;i++)
            {
                double mid=(lo+hi)/2;CapacityPoint n=DirectionalSolver.At(solver,mid,psi,p.AngleCount);if(n==null)return null;
                q=Apply(n,p,threshold);double f=q.P-target;if(Math.Abs(f)<Math.Max(1e-7,(solver.PMax-solver.PMin)*1e-11))return q;
                if(f*fl>0){lo=mid;fl=f;}else hi=mid;
            }
            return q!=null&&Math.Abs(q.P-target)<1e-4?q:null;
        }
        internal static void Populate(Project p,Analysis r,SectionSolver solver,DirectionalDiagram d)
        {
            d.ReductionProfile=p.ReductionProfile;double compression=CompressionFactor(p),cap=CapFactor(p)*compression*r.PMax,threshold=Threshold(p,d);d.ReducedAxialCap=cap;
            // Clip, rather than clamp P coordinates: a capped interaction boundary has a horizontal top.
            foreach(List<CapacityPoint> path in d.Paths)
            {
                List<StrengthPoint> reduced=new List<StrengthPoint>();
                for(int i=0;i<path.Count;i++)
                {
                    StrengthPoint q=Apply(path[i],p,threshold);
                    if(i>0)
                    {
                        StrengthPoint prev=Apply(path[i-1],p,threshold);
                        if((prev.P-cap)*(q.P-cap)<0)
                        {
                            StrengthPoint crossing=Solve(p,solver,d.MomentAngle,path[i-1],path[i],cap,threshold);
                            if(crossing!=null)reduced.Add(crossing);
                            if(q.P>cap&&reduced.Count>0){d.ReducedPaths.Add(reduced);reduced=new List<StrengthPoint>();}
                        }
                    }
                    if(q.P<=cap)reduced.Add(q);
                }
                if(reduced.Count>0)d.ReducedPaths.Add(reduced);
            }
            if(d.Load.P<=cap+1e-7&&d.Load.P>=TensionFactor(p)*r.PMin-1e-7)
            {
                List<StrengthPoint> roots=new List<StrengthPoint>();
                foreach(List<CapacityPoint> path in d.Paths)for(int i=0;i<path.Count;i++)
                {
                    StrengthPoint q=Apply(path[i],p,threshold),candidate=null;
                    if(Math.Abs(q.P-d.Load.P)<1e-7)candidate=q;
                    else if(i>0&&(Apply(path[i-1],p,threshold).P-d.Load.P)*(q.P-d.Load.P)<0)candidate=Solve(p,solver,d.MomentAngle,path[i-1],path[i],d.Load.P,threshold);
                    if(candidate!=null)
                    {
                        bool duplicate=false;foreach(StrengthPoint root in roots)if(Math.Abs(M(root)-M(candidate))<1e-5+M(candidate)*1e-8)duplicate=true;
                        if(!duplicate)roots.Add(candidate);
                        if(d.ReducedAtDemand==null||M(candidate)<M(d.ReducedAtDemand))d.ReducedAtDemand=candidate;
                    }
                }
                d.ReducedIntersectionCount=roots.Count;
            }
            // The axial cap segment is a code boundary, not a fictitious strain state.
            bool originInside=Engine.InsideSlice(solver.Slice(cap/compression,p.AngleCount*2),0,0);
            if(originInside)foreach(var path in d.ReducedPaths)
            {
                if(path.Count==0)continue;StrengthPoint first=path[0],last=path[path.Count-1];
                if(Math.Abs(first.P-cap)<1e-4)path.Insert(0,new StrengthPoint{P=cap,Phi=compression});
                if(Math.Abs(last.P-cap)<1e-4)path.Add(new StrengthPoint{P=cap,Phi=compression});
            }
            Recheck(p,solver,d);
        }
        internal static void Recheck(Project p,SectionSolver solver,DirectionalDiagram d)
        {
            if(d.ReducedAtDemand==null||!d.HasMoment)return;
            StrengthPoint q=d.ReducedAtDemand;Demand trial=new Demand{Name=d.Load.Name,P=q.Nominal.P,Mx=d.Load.Mx/q.Phi,My=d.Load.My/q.Phi};
            DemandResult fine=Engine.Check(trial,solver.Slice(trial.P,p.AngleCount*2)),coarse=Engine.Check(trial,solver.Slice(trial.P,p.AngleCount));
            if(fine.Status!=coarse.Status||fine.Utilization.HasValue&&coarse.Utilization.HasValue&&Math.Abs(fine.Utilization.Value-coarse.Utilization.Value)>0.01*Math.Max(1e-9,fine.Utilization.Value))fine.Status="Unresolved";
            d.ReducedCheck=fine;
        }
    }
}
