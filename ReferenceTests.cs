using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PMStudio;

static class ReferenceTests
{
    static int count;
    const double Kip=4.4482216152605,KipFt=1.3558179483314;
    static void Yes(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS REFERENCE "+text);}
    static void Near(double actual,double expected,double tol,string text){Yes(Math.Abs(actual-expected)<=tol,text+" actual="+actual.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" published="+expected);}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            foreach(bool high in new[]{false,true})
            {
                Project p=Examples.AciReference(high);Engine.Validate(p);double ey=p.Material.Fy/p.Material.Es;
                double[] strains={0,ey/2,ey,ey+0.003};
                // StructurePoint 2022 published tables, spColumn column. Tolerances reflect printed precision.
                double[] pf=high?new[]{650.6,377.0,175.4,67.0}:new[]{622.3,421.9,270.9,171.6};
                double[] mf=high?new[]{182.84,235.45,264.58,306.96}:new[]{169.86,220.05,250.77,286.75};
                string name=high?"Grade80":"Grade60";
                CapacityPoint compression=Engine.Evaluate(p,90,double.PositiveInfinity,false),tension=Engine.Evaluate(p,90,0,false);
                Near(Reduction.Apply(compression,p.Material).P/Kip,high?1101.1:997.1,0.051,name+" reduced pure compression (before cap)");
                Near(0.8*0.65*compression.P/Kip,high?880.9:797.7,0.051,name+" axial cap");
                Near(-Reduction.Apply(tension,p.Material).P/Kip,high?576:432,1e-6,name+" pure tension");
                for(int i=0;i<strains.Length;i++)
                {
                    double c=13.5*25.4*0.003/(0.003+strains[i]);CapacityPoint q=Engine.Evaluate(p,90,c,true);StrengthPoint reduced=Reduction.Apply(q,p.Material);
                    Near(reduced.P/Kip,pf[i],0.051,name+" control"+i+" phiPn kip");Near(reduced.Mx/KipFt,mf[i],0.011,name+" control"+i+" phiMn kip-ft");
                    Near(q.MinSteelStrain,-strains[i],1e-12,name+" control"+i+" requested steel strain");
                }
                CapacityPoint pure=DirectionalSolver.AtP(p,0,0);Near(Reduction.Apply(pure,p.Material).Mx/KipFt,high?278.96:213.91,0.011,name+" pure bending phiMn kip-ft");
                CapacityPoint mid=Engine.Evaluate(p,90,13.5*25.4*0.003/(0.003+ey+0.0015),false);Near(Reduction.Factor(mid,p.Material),0.775,1e-10,name+" transition phi from independent linear relation");
                Analysis r=Engine.Analyze(p);Yes(r.ReportDiagrams.All(d=>d.ReducedPaths.Count>0),name+" both capacity curves generated");
                foreach(var d in r.ReportDiagrams)
                {
                    Yes(d.ReducedPaths.SelectMany(q=>q).All(q=>q.P<=d.ReducedAxialCap.Value+1e-4),name+" curve clipped at axial cap");
                    foreach(StrengthPoint q in d.ReducedPaths.SelectMany(path=>path).Where(q=>q.Nominal!=null).Where((q,i)=>i%23==0))
                    {
                        Near(q.P,q.Phi*q.Nominal.P,1e-8,name+" reduce both axial and moment coordinates P");Near(q.Mx,q.Phi*q.Nominal.Mx,1e-8,name+" reduce both axial and moment coordinates Mx");
                    }
                    if(d.ReducedAtDemand!=null)Near(d.ReducedAtDemand.P,d.Load.P,1e-5,name+" reduced capacity solved at exact DEMAND P (not phi times nominal at demand P)");
                }
                var rows=Reports.Summary(r);for(int i=0;i<3;i++)if(rows[i].ReducedMn.HasValue){Near(rows[i].ReducedMn.Value,Math.Sqrt(r.ReportDiagrams[i].ReducedAtDemand.Mx*r.ReportDiagrams[i].ReducedAtDemand.Mx+r.ReportDiagrams[i].ReducedAtDemand.My*r.ReportDiagrams[i].ReducedAtDemand.My),1e-8,name+" table matches blue C point");}
                using(ReportSheet sheet=new ReportSheet()){sheet.Bind(p,r);sheet.SaveImage(Path.Combine(args[0],high?"reference-grade80.png":"reference-grade60.png"),2400,2700);sheet.SaveTableImage(Path.Combine(args[0],high?"reference-grade80-table.png":"reference-grade60-table.png"));Yes(sheet.SummaryGrid.Columns[4].HeaderText=="φMn / Mu",name+" UI table explicitly reference factor ratio");}
                File.WriteAllText(Path.Combine(args[0],high?"reference-grade80.pmproj.json":"reference-grade60.pmproj.json"),Files.Json(p));Reports.SummaryCsv(Path.Combine(args[0],high?"reference-grade80-summary.csv":"reference-grade60-summary.csv"),r);
                // Same geometry, different displacement integration: preserve and disclose a modelling difference.
                Project disk=Files.Clone(p);disk.BarDisplacement="ExactDisk";CapacityPoint exact=DirectionalSolver.AtP(disk,0,0);Console.WriteLine("MODEL "+name+" pure bending ExactDisk_phiMn="+(Reduction.Apply(exact,disk.Material).Mx/KipFt).ToString("F6")+" vs CenterLumped_phiMn="+(Reduction.Apply(pure,p.Material).Mx/KipFt).ToString("F6"));
            }
            Project asym=Examples.Rectangle();asym.Bars.RemoveAt(0);CapacityPoint pos=DirectionalSolver.AtP(asym,3000,0),neg=DirectionalSolver.AtP(asym,3000,180);Yes(Math.Abs(pos.Mx+neg.Mx)>1,"opposite bending capacities of asymmetric section are NOT mirrored");
            // Synthetic points only test degeneracy handling, not an engineering reference.
            Analysis ambiguous=new Analysis();ambiguous.ReportDiagrams.Add(new DirectionalDiagram{HasMoment=true,ReductionProfile="Reference-ACI31819-Tied",ReducedIntersectionCount=2,Load=new Demand{Name="loop",P=1,Mx=1},AtDemand=new CapacityPoint{Mx=10},ReducedAtDemand=new StrengthPoint{P=1,Mx=2},ReducedCheck=new DemandResult{Status="InsideNominal",Utilization=0.5},Check=new DemandResult{Status="InsideNominal",Utilization=0.1}});Yes(Reports.Summary(ambiguous)[0].ReducedStatus=="미확정","multiple design intersections cannot produce automatic OK");
            Console.WriteLine("PASS REFERENCE total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
