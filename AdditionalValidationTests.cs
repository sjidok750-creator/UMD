using System;using System.Collections.Generic;using System.Drawing;using System.Globalization;using System.IO;using System.Linq;using System.Text;using PMStudio;
static class AdditionalValidationTests
{
 const double Kip=4.4482216152605,KipFt=1.3558179483314;
 static int count;
 static readonly List<object> states=new List<object>();
 static readonly StringBuilder source=new StringBuilder("Example,State,Quantity,Published,Calculated,Difference,RelativeDifference,Comparison,Source\r\n");
 static string F(double value){return value.ToString("G17",CultureInfo.InvariantCulture);}
 static void Yes(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS ADDITIONAL "+name);}
 static void Near(double actual,double expected,double tolerance,string name){Yes(Math.Abs(actual-expected)<=tolerance,name+" actual="+F(actual)+" expected="+F(expected));}
 static void Record(string ex,string name,string quantity,double original,double actual,string status,string location)
 {source.AppendLine(string.Join(",",new[]{ex,name,quantity,F(original),F(actual),F(actual-original),original==0?"":F((actual-original)/Math.Abs(original)),status,location}));}
 static void Rounded(string name,string quantity,double expected,double actual,string location)
 {Near(actual,expected,.501,name+" "+quantity+" within original integer rounding");Record("SPWall",name,quantity,expected,actual,"Matches_printed_rounding",location);}
 static void State(string name,Project p,CapacityPoint q){states.Add(new{Name=name,Project=p,Point=q});}
 [STAThread]static int Main(string[] args){try{
 string folder=args[0];Project aci=AdditionalExamples.AciE702();Engine.Validate(aci);
 double[] zs={.9,-.5,-1.1,-5},pubP={658.98,281.02,160.96,-43.73},pubM={2.82,99.41,105.45,52.38};
 for(int i=0;i<zs.Length;i++)
 {
     double c=9.5*25.4*.003/(.003-zs[i]*aci.Material.Fy/aci.Material.Es);CapacityPoint q=Engine.Evaluate(aci,90,c,true);string name="Z="+F(zs[i]);State("E702 "+name,aci,q);
     Record("ACI_E702",name,"Pn_kip",pubP[i],q.P/Kip,i<2?"Matches_printed_rounding":"Intermediate_rounding_difference","PDF"+new[]{7,9,11,13}[i]);
     Record("ACI_E702",name,"Mn_kipft",pubM[i],q.Mx/KipFt,i==0?"Source_formula_result_inconsistent":i==2?"Matches_printed_rounding":"Intermediate_rounding_difference","PDF"+new[]{7,9,11,13}[i]);
 }
 // Independent force/moment sums from the stated unrounded material and bar data.
 var full=Engine.Evaluate(aci,90,double.PositiveInfinity,true);
 Near(full.P/Kip,.85*4*(144-3.16)+3.16*60,1e-8,"E702 uniform compression closed form");
 Near(.8*.65*full.P/Kip,347.60,.0051,"E702 printed compression cap");
 var q09=Engine.Evaluate(aci,90,9.5*25.4*.003/(.003-.9*60/29000),true);
 Near(q09.Mx/KipFt,((60-3.4)*1.58-(54-3.4)*1.58)*3.5/12,1e-8,"E702 Z0.9 force couple gives 2.765 (source prints 2.82)");
 Near((489.60*3.5+89.43*7-658.98*3.5)/12,2.765,1e-12,"source printed Z0.9 equation evaluates to 2.765");
 // Deliberate replay of the source's rounded Z=-5 intermediate values.
 double pPrinted=-94.8-23.15+74.22,mPrinted=(74.22*8.59-23.15*7-pPrinted*3.5)/12;
 Near(pPrinted,-43.73,1e-10,"E702 Z-5 rounded force replay");Near(mPrinted,52.38,.0051,"E702 Z-5 rounded moment-arm replay");
 Record("ACI_E702","Z=-5 printed replay","Pn_kip",-43.73,pPrinted,"Printed_intermediates_reproduce_source","PDF12-13");
 Record("ACI_E702","Z=-5 printed replay","Mn_kipft",52.38,mPrinted,"Printed_intermediates_reproduce_source","PDF12-13");
 Project wall=AdditionalExamples.UnsymmetricalWall();Engine.Validate(wall);RegionIntegral gross=Geometry.Region(wall,0,1,0,true);
 Near(gross.Area/(25.4*25.4),7576,1e-7,"wall original concrete area");Near(wall.Bars.Sum(b=>b.Area)/(25.4*25.4),43.88,1e-10,"wall original hand-table steel area");
 double[,] expectedP={{15461,12520,10658,11151},{11614,8418,6300,5105}},expectedM={{52237,85349,97424,138234},{77723,95119,98452,116489}};
 double[] strains={0,wall.Material.Fy/wall.Material.Es/2,wall.Material.Fy/wall.Material.Es,.005};
 for(int direction=0;direction<2;direction++)for(int i=0;i<4;i++)
 {
     double angle=direction*180,c=395.5*25.4*.003/(.003+strains[i]),phi=i==3?.9:.65;
     var q=Engine.Evaluate(wall,angle,c,true);string name=(direction==0?"negative_My":"positive_My")+" control"+i;
     Rounded(name,"phiPn_kip",expectedP[direction,i],phi*q.P/Kip,"PDF"+(direction==0?32:34));
     Rounded(name,"phiMn_kipft",expectedM[direction,i],Math.Abs(phi*q.My/KipFt),"PDF"+(direction==0?32:34));
     Near(q.MinSteelStrain,-strains[i],1e-12,name+" actual extreme bar strain");Near(q.Mx,0,1e-7,name+" orthogonal equilibrium");State("Wall "+name,wall,q);
 }
 foreach(double angle in new[]{0.0,180})
 {
     var q=Engine.AtP(wall,angle,0);string name=angle==0?"negative_My":"positive_My";double reported=angle==0?27396:48226;
     Near(q.P,0,1e-6,name+" exact pure bending axial equilibrium");
     var direction=DirectionalSolver.AtP(wall,0,angle==0?270:90);Near(direction.My,q.My,1e-5,name+" fixed moment direction and fixed normal agree");
     Record("SPWall",name+" pure bending","phiMn_kipft",reported,.9*Math.Abs(q.My)/KipFt,"Source_zero_force_solution_difference","PDF"+(angle==0?32:34));
     State("Wall pure "+name,wall,q);
     var printedDepth=Engine.Evaluate(wall,angle,(angle==0?4.3:28.1)*25.4,true);
     Record("SPWall",name+" printed c","Pn_kip",0,printedDepth.P/Kip,"Printed_depth_is_not_exact_zero_force","PDF"+(angle==0?32:34));State("Wall printed pure c "+name,wall,printedDepth);
 }
 var wc=Engine.Evaluate(wall,0,double.PositiveInfinity,true);var wt=Engine.Evaluate(wall,0,0,true);
 Rounded("uniform_compression","phiPn_kip",18357,.65*wc.P/Kip,"PDF32");Rounded("uniform_compression","phiMn_kipft",2605,Math.Abs(.65*wc.My/KipFt),"PDF32");
 Rounded("uniform_tension","phiPn_magnitude_kip",2370,-.9*wt.P/Kip,"PDF32");Rounded("uniform_tension","phiMn_kipft",3824,Math.Abs(.9*wt.My/KipFt),"PDF32");
 Yes(Math.Abs(wc.My)>100&&Math.Abs(wt.My)>100,"asymmetric uniform end states retain nonzero centroid moments");
 // A separate area/first-moment checker reads all of these states without using this DLL.
 var ring=Examples.Circle(true);var asym=Examples.Rectangle();asym.Bars.RemoveAt(0);
 Project[] projects={aci,wall,AdditionalExamples.HollowBox(),Examples.CircularReference(),ring,asym};string[] names={"e702","wall","offset_box","circle","polygon_ring","asym_rectangle"};
 double[] angles={0,17,43,90,133,180,227,271},fractions={0,.035,.1,.25,.5,.85,1.5,8,double.PositiveInfinity};
 for(int j=0;j<projects.Length;j++)
 {
     var p=projects[j];Engine.Validate(p);int n=0;
     foreach(double angle in angles)
     {
         double nx=Math.Cos(angle*Math.PI/180),ny=Math.Sin(angle*Math.PI/180);var coords=p.Contours.Where(c=>!c.Hole).SelectMany(c=>c.Points).Select(v=>nx*v.X+ny*v.Y).ToArray();double span=coords.Max()-coords.Min();
         foreach(double fraction in fractions){State(names[j]+" angle"+angle+" depth"+F(fraction),p,Engine.Evaluate(p,angle,span*fraction,false));n++;}
     }
     Yes(n==72,names[j]+" independent-check grid exported: 72 finite/uniform states");
     // Exact demand P and direction solved by the production engine, then checked independently.
     double pmax=Engine.Evaluate(p,0,double.PositiveInfinity,false).P,pmin=Engine.Evaluate(p,0,0,false).P;
     foreach(double axial in new[]{.5*pmin,0,.25*pmax,.6*pmax})foreach(double angle in new[]{0.0,37,123,217})
     {
         var q=DirectionalSolver.AtP(p,axial,angle);if(q==null)continue;
         Near(q.P,axial,Math.Max(1e-5,(pmax-pmin)*1e-10),names[j]+" solved P at psi"+angle);
         double cross=q.My*Math.Cos(angle*Math.PI/180)-q.Mx*Math.Sin(angle*Math.PI/180);Near(cross,0,1e-5,names[j]+" solved moment direction "+angle);State(names[j]+" solved P"+F(axial)+" psi"+angle,p,q);
     }
     File.WriteAllText(Path.Combine(folder,"additional-"+names[j]+".pmproj.json"),Files.Json(p));
 }
 File.WriteAllText(Path.Combine(folder,"additional-source-comparison.csv"),source.ToString(),new UTF8Encoding(true));
 File.WriteAllText(Path.Combine(folder,"additional-independent-states.json"),Files.Json(states),new UTF8Encoding(false));
 Console.WriteLine("PASS ADDITIONAL total: "+count);Console.WriteLine("Independent states: "+states.Count);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
