using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PMStudio;

static class Manual2008Tests
{
    static int count;
    static void Yes(bool value,string text){if(!value)throw new Exception(text);count++;Console.WriteLine("PASS MANUAL "+text);}
    static string N(double n){return n.ToString("G17",CultureInfo.InvariantCulture);}
    static void Near(double a,double b,double tol,string text){Yes(Math.Abs(a-b)<=tol,text+" actual="+N(a)+" reference="+N(b));}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string folder=args[0];Project p=Examples.Manual2008();Engine.Validate(p);
            double ag=Math.PI*1000*1000,ast=95*506.7,cap=0.8*0.7*(0.85*24*(ag-ast)+300*ast)/1000;
            Analysis r=Engine.Analyze(p);Near(r.GrossArea,3141592.654,0.001,"handbook PDF847 gross circle area");Near(r.SteelArea,48136.500,1e-7,"handbook PDF847 steel area");Near(r.ReducedCap(),43426.575,0.001,"handbook PDF846 maximum reduced compression");
            Near(r.ReducedCap(),cap,1e-8,"independent closed-form cap");Near(ast/ag,0.01532,0.000005,"handbook printed reinforcement ratio");
            // Exact circle segment checked independently against a one-million-strip integral.
            RegionIntegral clipped=Geometry.CircleSegment(230,-310,1000,0,1,-10,false);double stripArea=0,stripQ=0,dy=700.0/1000000;
            for(int i=0;i<1000000;i++){double y=300+(i+0.5)*dy,w=2*Math.Sqrt(1000000-y*y);stripArea+=w*dy;stripQ+=w*y*dy;}
            Near(clipped.Area,stripArea,0.01,"analytic circular compression area vs independent strips");Near(clipped.Qy,-310*stripArea+stripQ,5,"analytic circular first moment vs independent strips");
            CapacityPoint normal=Engine.Evaluate(p,90,1200,false);Project moved=Files.Clone(p);foreach(var c in moved.Contours)foreach(var v in c.Points){v.X+=230;v.Y-=310;}foreach(var b in moved.Bars){b.X+=230;b.Y-=310;}
            CapacityPoint translated=Engine.Evaluate(moved,90,1200,false);Near(normal.P,translated.P,1e-6,"analytic circle translation P");Near(normal.Mx,translated.Mx,1e-5,"analytic circle translation Mx");
            // Published PHI-P / PHI-M pairs are not the fixed-P capacities at Pu.
            // Compare moment on our actual curve at each published phiPn; record differences explicitly.
            string[] labels={"transverse LC1","transverse LC2","transverse LC3","transverse LC4","longitudinal LC3","combined LC3","combined LC4"};
            double[] pp={43426.575,40774.900,20513.734,37260.496,10490.371,10230.336,38890.251};
            double[] mm={7677.968,9019.580,14298.330,10555.010,12651.690,12570.030,9979.806};
            StringBuilder csv=new StringBuilder("Item,HandbookValue,CalculatedValue,RelativeDifference,Unit,Comparison,Source\r\n");
            csv.AppendLine("Ag,3141592.654,"+N(r.GrossArea)+","+N(r.GrossArea/3141592.654-1)+",mm2,Matches_printed_rounding,PDF847_body509-129");
            csv.AppendLine("Ast,48136.5,"+N(r.SteelArea)+",0,mm2,Matches_printed_rounding,PDF847_body509-129");
            csv.AppendLine("phiPn_cap,43426.575,"+N(r.ReducedCap())+","+N(r.ReducedCap()/43426.575-1)+",kN,Matches_printed_rounding,PDF846_body509-128");
            double max=0;
            for(int i=0;i<pp.Length;i++)
            {
                CapacityPoint q=DirectionalSolver.AtP(p,pp[i]/0.7,0);double actual=q.Mx*0.7,delta=actual/mm[i]-1;max=Math.Max(max,Math.Abs(delta));
                Near(q.P*0.7,pp[i],1e-5,"published axial coordinate equilibrium "+labels[i]);
                csv.AppendLine(labels[i]+","+N(mm[i])+","+N(actual)+","+N(delta)+",kNm,Difference_not_exact_reproduction,PDF846_body509-128");
                Console.WriteLine("COMPARE "+labels[i]+" phiM_reference="+N(mm[i])+" phiM_calculated="+N(actual)+" relative="+N(delta));
            }
            CapacityPoint balanced=r.ReportDiagrams[0].Balanced;double eb=Math.Sqrt(balanced.Mx*balanced.Mx+balanced.My*balanced.My)/balanced.P*1000;
            csv.AppendLine("balanced_e,545.084,"+N(eb)+","+N(eb/545.084-1)+",mm,Unresolved_difference,PDF846_body509-128");
            csv.AppendLine("transverse_LC2_e_from_printed_Mu_Pu,222.005,"+N(1000*1898.854/8595.965)+","+N((1000*1898.854/8595.965)/222.005-1)+",mm,Source_internal_inconsistency,PDF846_body509-128");
            csv.AppendLine("transverse_LC4_e_from_printed_Mu_Pu,284.102,"+N(1000*1813.264/6415.471)+","+N((1000*1813.264/6415.471)/284.102-1)+",mm,Source_internal_inconsistency,PDF846_body509-128");
            csv.AppendLine("d_from_weighted_cover_vs_figure,1852.526,"+N(2000-(50*100.0+45*200.0)/95)+","+N((2000-(50*100.0+45*200.0)/95)/1852.526-1)+",mm,Source_table_figure_inconsistency,PDF847_body509-129");
            Console.WriteLine("COMPARE balanced_e_reference=545.084 calculated="+N(eb)+" relative="+N(eb/545.084-1));
            Console.WriteLine("COMPARE max_abs_moment_difference="+N(max));
            Yes(Math.Abs(eb/545.084-1)>0.01,"balanced eccentricity mismatch is retained, not hidden as passing reference");
            // Printed d differs between table and figure. Neither replaces true extreme steel depth.
            double meanCover=(50*100.0+45*200.0)/95;Near(meanCover,147.368,0.001,"weighted cover from actual two rings");
            Near(balanced.MinSteelStrain,-300.0/200000,1e-8,"engine balanced state uses actual extreme bar yield");
            File.WriteAllText(Path.Combine(folder,"manual-2008-comparison.csv"),csv.ToString(),new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(folder,"manual-2008.pmproj.json"),Files.Json(p),new UTF8Encoding(false));Reports.Csv(Path.Combine(folder,"manual-2008-curves.csv"),p,r);Reports.SummaryCsv(Path.Combine(folder,"manual-2008-summary.csv"),r);Reports.Html(Path.Combine(folder,"manual-2008-result.html"),p,r);
            using(ReportDrawing drawing=new ReportDrawing())using(Bitmap image=drawing.Create(p,r,2400,2700))image.Save(Path.Combine(folder,"manual-2008-report.png"));
            using(ReportCaseCanvas plot=new ReportCaseCanvas{Project=p,Result=r,CaseIndex=1})using(Bitmap image=new Bitmap(810,506))using(Graphics g=Graphics.FromImage(image)){plot.Render(g,810,506);image.Save(Path.Combine(folder,"manual-2008-detail.png"));}
            Console.WriteLine("PASS MANUAL total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static double ReducedCap(this Analysis r){return r.ReportDiagrams[0].ReducedAxialCap.Value;}
}
