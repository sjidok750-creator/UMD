using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PMStudio;

static class ReportTests
{
    static int count;
    static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS REPORT "+message);}
    static double M(CapacityPoint q){return Math.Sqrt(q.Mx*q.Mx+q.My*q.My);}
    static void Near(double a,double b,double tolerance,string message){Check(Math.Abs(a-b)<=tolerance,message);}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string folder=args[0];Project p=Examples.Rectangle();Analysis r=Engine.Analyze(p);
            Check(r.ReportDiagrams.Count==3,"three governing combination diagrams");
            Check(r.ReportDiagrams[0].Load.Name=="ULS-02"&&r.ReportDiagrams[1].Load.Name=="ULS-03"&&r.ReportDiagrams[2].Load.Name=="ULS-03","max P and max ABS Mx/My preserve combination IDs");
            foreach(DirectionalDiagram d in r.ReportDiagrams)
            {
                Demand input=p.Loads.Single(l=>l.Name==d.Load.Name);Check(input.P==d.Load.P&&input.Mx==d.Load.Mx&&input.My==d.Load.My,"simultaneous P Mx My values "+d.Title);
                double a=d.MomentAngle*Math.PI/180;var pts=d.Paths.SelectMany(path=>path).ToArray();
                Check(pts.Length>130,"detailed actual equilibrium samples "+pts.Length);
                Check(pts.All(q=>Math.Abs(q.My*Math.Cos(a)-q.Mx*Math.Sin(a))<1e-5),"every curve point satisfies moment direction equilibrium");
                Near(d.AtDemand.P,input.P,1e-5,"capacity at exact load axial force");Near(d.Balanced.MinSteelStrain,-p.Material.Fy/p.Material.Es,1e-9,"directional balanced steel yield");Near(d.PureBending.P,0,1e-5,"directional pure bending equilibrium");
                Check(d.Paths.All(path=>path.Zip(path.Skip(1),(left,right)=>right.P>=left.P).All(v=>v)),"paths ordered by axial force");
                Check(!d.ResolutionLimited&&d.MaxChordError<=0.00025,"adaptive midpoint chord criterion reached");
                CapacityPoint check=Engine.Evaluate(p,d.AtDemand.Angle,d.AtDemand.Depth<0?double.PositiveInfinity:d.AtDemand.Depth,true);
                Near(check.Mx,d.AtDemand.Mx,1e-8,"capacity stored from material integration");
                // Quarter points were not the midpoint stopping test: independently measure
                // actual moment error over a representative subset of retained intervals.
                double maxError=0,scale=pts.Max(q=>M(q));
                foreach(var path in d.Paths)for(int i=1;i<path.Count;i+=Math.Max(1,path.Count/12))
                {
                    CapacityPoint l=path[i-1],h=path[i];double pp=l.P+(h.P-l.P)*0.25;CapacityPoint q=DirectionalSolver.AtP(p,pp,d.MomentAngle);if(q==null)continue;
                    maxError=Math.Max(maxError,Math.Abs(M(q)-(M(l)*0.75+M(h)*0.25))/scale);
                }
                Check(maxError<0.002,"independent quarter-level curve samples <0.2% of moment scale (sampled intervals)");
            }
            var rows=Reports.Summary(r);Check(rows[0].Status=="O.K. (공칭)"&&rows[1].Status=="N.G. (공칭)"&&rows[2].Status=="N.G. (공칭)","summary nominal OK NG follows actual combinations");
            for(int i=0;i<3;i++)Near(rows[i].NominalMn.Value,M(r.ReportDiagrams[i].AtDemand),1e-10,"table capacity equals plotted C");
            Near(rows[0].Mu,Math.Sqrt(650*650+250*250),1e-10,"table Mu is actual biaxial resultant");
            Near(rows[0].NominalCapacityDemandRatio.Value,rows[0].NominalMn.Value/rows[0].Mu,1e-12,"capacity over demand ratio, not its reciprocal");
            CapacityPoint axis=DirectionalSolver.AtP(p,1500,0);CapacityPoint old=Engine.AxisIntersections(p,1500,Engine.Slice(p,1500,72),true).OrderByDescending(q=>q.Mx).First();Near(axis.Mx,old.Mx,1e-6,"direction zero matches independently selected My=0 axis root");
            CapacityPoint oblique=DirectionalSolver.AtP(p,3000,32);Check(Math.Abs((oblique.Angle+90)%180-32)>1,"neutral axis orientation distinct from moment direction");
            Project dense=Files.Clone(p);dense.AngleCount=144;CapacityPoint doubled=DirectionalSolver.AtP(dense,3000,32);Near(oblique.Mx,doubled.Mx,1e-5,"direction root invariant under doubled search subdivisions");
            Project shifted=Files.Clone(p);foreach(var c in shifted.Contours)foreach(var v in c.Points){v.X+=530;v.Y-=300;}foreach(var b in shifted.Bars){b.X+=530;b.Y-=300;}
            CapacityPoint translated=DirectionalSolver.AtP(shifted,3000,32);Near(oblique.Mx,translated.Mx,1e-5,"directional translation invariance");
            // Force uncertainty and zero demand without manufacturing code pass results.
            r.ReportDiagrams[0].ResolutionLimited=true;Check(Reports.Summary(r)[0].Status=="미확정","refinement limit suppresses OK");r.ReportDiagrams[0].ResolutionLimited=false;
            DemandResult before=r.ReportDiagrams[0].Check;r.ReportDiagrams[0].Check=new DemandResult{Status="Unresolved"};Check(Reports.Summary(r)[0].Status=="미확정","angular uncertainty suppresses OK");r.ReportDiagrams[0].Check=before;
            Project zero=Files.Clone(p);zero.Loads.Clear();zero.Loads.Add(new Demand{Name="ZERO",P=1500});Analysis zr=Engine.Analyze(zero);Check(Reports.Summary(zr).All(q=>q.Status=="—"&&!q.NominalCapacityDemandRatio.HasValue),"zero demand ratio is undefined");
            Project empty=Files.Clone(p);empty.Loads.Clear();Check(Engine.Analyze(empty).ReportDiagrams.Count==0,"no invented load cases");
            Project pier=Examples.Pier();Engine.Validate(pier);Analysis pr=Engine.Analyze(pier);Check(pr.ReportDiagrams.All(d=>d.SolvedPoints>130),"concave pier directional report curves");
            Check(pr.ReportDiagrams.SelectMany(d=>d.Paths).SelectMany(q=>q).All(q=>Engine.Finite(q.P)&&Engine.Finite(q.Mx)&&Engine.Finite(q.My)),"pier curve all finite");
            File.WriteAllText(Path.Combine(folder,"pier-demo.pmproj.json"),Files.Json(pier));Reports.SummaryCsv(Path.Combine(folder,"pier-summary.csv"),pr);Reports.Csv(Path.Combine(folder,"pier-curves.csv"),pier,pr);Reports.Html(Path.Combine(folder,"pier-result.html"),pier,pr);
            Check(File.ReadAllText(Path.Combine(folder,"pier-summary.csv")).Contains("Nominal_Mn_over_Mu"),"CSV explicitly nominal capacity/demand");Check(File.ReadAllText(Path.Combine(folder,"pier-result.html")).Contains("Capacity Mn (공칭"),"HTML contains report summary table");
            using(ReportSheet sheet=new ReportSheet{Size=new Size(1200,900)})
            {
                sheet.CreateControl();sheet.Bind(pier,pr);Check(sheet.SummaryGrid.Rows.Count==3,"native report table three rows");
                Near(double.Parse(sheet.SummaryGrid.Rows[0].Cells[4].Value.ToString(),CultureInfo.CurrentCulture),Reports.Summary(pr)[0].NominalCapacityDemandRatio.Value,0.000051,"native grid ratio same as engine");
                sheet.SaveImage(Path.Combine(folder,"pier-report.png"),2400,2700);sheet.SaveTableImage(Path.Combine(folder,"pier-summary.png"));
                using(Bitmap image=new Bitmap(Path.Combine(folder,"pier-report.png"))){Check(image.Width==2400&&image.Height==2700&&Math.Abs(image.HorizontalResolution-300)<1,"300dpi report PNG correct dimensions");}
                using(Bitmap image=new Bitmap(Path.Combine(folder,"pier-summary.png")))Check(image.Width==2400&&image.Height==740,"separate table PNG dimensions");
                sheet.Bind(null,null);Check(sheet.SummaryGrid.Rows.Count==0,"input invalidation clears report table");
            }
            Console.WriteLine("REPORT points: "+string.Join(", ",pr.ReportDiagrams.Select(d=>d.SolvedPoints.ToString()).ToArray()));
            foreach(ReportRow row in Reports.Summary(pr))Console.WriteLine("TABLE "+row.Division+" Mu="+row.Mu.ToString("F6",CultureInfo.InvariantCulture)+" Mn="+row.NominalMn+" ratio="+row.NominalCapacityDemandRatio+" "+row.Status);
            Console.WriteLine("PASS REPORT total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
