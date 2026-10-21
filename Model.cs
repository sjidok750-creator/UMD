using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PMStudio
{
    public sealed class Point2
    {
        public double X, Y;
        public Point2() { }
        public Point2(double x, double y) { X = x; Y = y; }
    }
    public sealed class Contour
    {
        public bool Hole;
        public List<Point2> Points = new List<Point2>();
    }
    public sealed class Rebar
    {
        public double X, Y, Area;
        public Rebar() { }
        public Rebar(double x, double y, double a) { X=x; Y=y; Area=a; }
    }
    public sealed class Demand
    {
        public string Name="LC1";
        public double P, Mx, My;
    }
    public sealed class Material
    {
        public double Fc=30, Fy=400, Es=200000, EpsCu=0.003, Alpha=0.85, Beta=0.85;
    }
    public sealed class Project
    {
        public string Format="PMStudio.Project", Version="1.0", Name="RC 교각 검토";
        public string Model="RC-RectangularBlock-Nominal-v1";
        public string Units="mm-MPa-kN-kNm";
        public string ReductionProfile="Nominal",BarDisplacement="ExactDisk";
        public double CircularRadius;
        public Material Material=new Material();
        public SectionRecipe SectionInput;
        public List<Contour> Contours=new List<Contour>();
        public List<Rebar> Bars=new List<Rebar>();
        public List<Demand> Loads=new List<Demand>();
        public int AngleCount=72, DepthCount=90;
        public double SliceP=1500;
    }
    public sealed class BarState
    {
        public double X,Y,Area,Strain,Stress,Force;
    }
    public sealed class CapacityPoint
    {
        public double P, Mx, My, Angle, Depth, BlockDepth, ConcreteArea, ConcreteForce, SteelForce, MinSteelStrain, MaxSteelStrain;
        public string State="";
        public List<BarState> Bars;
    }
    public sealed class DemandResult
    {
        public string Name, Status, Method="Fixed-P moment-ray intersection";
        public double P,Mx,My;
        public double? Utilization, CapacityMx, CapacityMy;
        public string Note;
    }
    public sealed class Analysis
    {
        public string Engine="PMStudio.Core/0.5.0", Model="RC-RectangularBlock-Nominal-v1";
        public string Units="mm-MPa-kN-kNm", Strength="Nominal; no design-code factors or axial caps";
        public string Convention="P compression positive; Mx=integral(sigma*(y-yc)); My=-integral(sigma*(x-xc))";
        public double GrossArea, SteelArea, Cx,Cy, PMin,PMax, SliceP, AngularChange;
        public List<List<CapacityPoint>> Curves=new List<List<CapacityPoint>>();
        public List<List<CapacityPoint>> DisplayRings=new List<List<CapacityPoint>>();
        public SectionLimits Limits=new SectionLimits();
        public List<List<CapacityPoint>> PMxPaths=new List<List<CapacityPoint>>();
        public List<List<CapacityPoint>> PMyPaths=new List<List<CapacityPoint>>();
        public List<CapacityPoint> Slice=new List<CapacityPoint>();
        public List<DemandResult> Demands=new List<DemandResult>();
        public List<DirectionalDiagram> ReportDiagrams=new List<DirectionalDiagram>();
        public List<string> Notes=new List<string>();
    }
    public sealed class DirectionalDiagram
    {
        public string Title,Note;
        public Demand Load;
        public double MomentAngle,MaxChordError;
        public bool HasMoment,ResolutionLimited;
        public int SolvedPoints;
        public List<List<CapacityPoint>> Paths=new List<List<CapacityPoint>>();
        public CapacityPoint Balanced,PureBending,AtDemand;
        public string ReductionProfile="Nominal";
        public double? ReducedAxialCap;
        public List<List<StrengthPoint>> ReducedPaths=new List<List<StrengthPoint>>();
        public StrengthPoint ReducedAtDemand;
        public DemandResult ReducedCheck;
        public int ReducedIntersectionCount;
        public DemandResult Check;
    }
    public sealed class StrengthPoint
    {
        public double P,Mx,My,Phi;
        public CapacityPoint Nominal;
    }
    public sealed class ReportRow
    {
        public string Division,Combination,Status,Note;
        public double P,Mx,My,Mu;
        public double? NominalMn,NominalCapacityDemandRatio;
        public double? ReducedMn,ReducedCapacityDemandRatio;
        public string ReducedStatus="미적용";
    }
    public sealed class SectionLimits
    {
        public double YieldStrain;
        public List<CapacityPoint> BalancedMx=new List<CapacityPoint>(),BalancedMy=new List<CapacityPoint>();
        public List<CapacityPoint> PureBendingMx=new List<CapacityPoint>(),PureBendingMy=new List<CapacityPoint>();
    }
    public static class Files
    {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=16000000, RecursionLimit=100 }; }
        public static string Json(object x) { return Serializer().Serialize(x); }
        public static Project Parse(string s)
        {
            Project p=Serializer().Deserialize<Project>(s);
            Engine.Validate(p); return p;
        }
        public static Project Clone(Project p) { return Parse(Json(p)); }
        public static string N(double n) { return n.ToString("G17",CultureInfo.InvariantCulture); }
    }
    public static class Examples
    {
        public static Project Rectangle()
        {
            Project p=new Project();
            p.Contours.Add(Polygon(false,-300,-400,300,-400,300,400,-300,400));
            double a=RebarCatalog.Get("D25").Area;
            foreach(double x in new double[]{-240,-80,80,240}) { p.Bars.Add(new Rebar(x,-340,a)); p.Bars.Add(new Rebar(x,340,a)); }
            foreach(double y in new double[]{-113.3333333333,113.3333333333}) { p.Bars.Add(new Rebar(-240,y,a));p.Bars.Add(new Rebar(240,y,a)); }
            p.Loads.Add(new Demand{Name="ULS-01",P=1500,Mx=500,My=120});
            p.Loads.Add(new Demand{Name="ULS-02",P=3000,Mx=-650,My=250});
            p.Loads.Add(new Demand{Name="ULS-03",P=1500,Mx=1400,My=600});
            return p;
        }
        public static Project Circle(bool hollow)
        {
            Project p=new Project{Name=hollow?"중공 원형 교각":"원형 교각",SliceP=2000,CircularRadius=hollow?0:500};
            p.Contours.Add(CircleContour(500,false));
            if(hollow) p.Contours.Add(CircleContour(280,true));
            for(int i=0;i<16;i++) { double t=i*2*Math.PI/16;p.Bars.Add(new Rebar(420*Math.Cos(t),420*Math.Sin(t),RebarCatalog.Get("D29").Area)); }
            p.Loads.Add(new Demand{Name="ULS-01",P=2000,Mx=700,My=400}); return p;
        }
        public static Contour CircleContour(double r,bool hole)
        {
            Contour c=new Contour{Hole=hole};
            for(int i=0;i<128;i++) { double t=i*2*Math.PI/128;c.Points.Add(new Point2(r*Math.Cos(t),r*Math.Sin(t))); }
            return c;
        }
        public static Project Pier()
        {
            Project p=new Project{Name="가상 예제 · 오목부가 있는 장원형 RC 교각 (실시설계 입력 아님)",SliceP=90000,AngleCount=144};
            Contour c=Polygon(false,-2200,800,-300,800,-300,500,300,500,300,800,2200,800);
            for(int i=1;i<=24;i++){double t=(90-i*180.0/24)*Math.PI/180;c.Points.Add(new Point2(2200+800*Math.Cos(t),800*Math.Sin(t)));}
            c.Points.AddRange(new[]{new Point2(300,-800),new Point2(300,-500),new Point2(-300,-500),new Point2(-300,-800),new Point2(-2200,-800)});
            for(int i=1;i<24;i++){double t=(270-i*180.0/24)*Math.PI/180;c.Points.Add(new Point2(-2200+800*Math.Cos(t),800*Math.Sin(t)));}p.Contours.Add(c);
            double area=RebarCatalog.Get("D32").Area;
            foreach(int sign in new[]{-1,1})
            {
                for(int x=500;x<=2100;x+=200)foreach(int side in new[]{-1,1})p.Bars.Add(new Rebar(side*x,sign*700,area));
                for(int x=-200;x<=200;x+=200)p.Bars.Add(new Rebar(x,sign*400,area));
                foreach(int side in new[]{-1,1})p.Bars.Add(new Rebar(side*400,sign*600,area));
                for(int a=-80;a<=80;a+=20){double t=(sign==1?a:180+a)*Math.PI/180;p.Bars.Add(new Rebar(sign*2200+690*Math.Cos(t),690*Math.Sin(t),area));}
            }
            p.Loads.Add(new Demand{Name="EX-Pmax",P=160000,Mx=25000,My=30000});
            p.Loads.Add(new Demand{Name="EX-Mxmax",P=90000,Mx=65000,My=20000});
            p.Loads.Add(new Demand{Name="EX-Mymax",P=70000,Mx=15000,My=380000});return p;
        }
        public static Project AciReference(bool highStrength)
        {
            const double psi=0.006894757293168;Project p=new Project{Name=highStrength?"공개 검증예제 · ACI 318-19 / Grade 80":"공개 검증예제 · ACI 318-19 / Grade 60",ReductionProfile="Reference-ACI31819-Tied",BarDisplacement="CenterLumped",SliceP=1200};
            p.Material=new Material{Fc=5000*psi,Fy=(highStrength?80000:60000)*psi,Es=29000000*psi,EpsCu=0.003,Alpha=0.85,Beta=0.8};
            double side=8*25.4,y=5.5*25.4;p.Contours.Add(Polygon(false,-side,-side,side,-side,side,side,-side,side));
            for(int i=0;i<4;i++){double x=(-5.5+11.0*i/3)*25.4;p.Bars.Add(new Rebar(x,-y,645.16));p.Bars.Add(new Rebar(x,y,645.16));}
            p.Loads.Add(new Demand{Name="EX-Pmax",P=3000,Mx=130,My=0});p.Loads.Add(new Demand{Name="EX-Mxmax",P=1200,Mx=370,My=0});p.Loads.Add(new Demand{Name="EX-Mymax",P=1000,Mx=40,My=400});return p;
        }
        public static Project Manual2008()
        {
            Project p=new Project{Name="도로설계편람 2008 · 원형 교각 검산",CircularRadius=1000,ReductionProfile="Reference-KR2008-Tied",BarDisplacement="CenterLumped",SliceP=7267.45,AngleCount=144};
            p.Material=new Material{Fc=24,Fy=300,Es=200000,EpsCu=0.003,Alpha=0.85,Beta=0.85};p.Contours.Add(CircleContour(1000,false));
            int[] numbers={50,45};double[] radii={900,800};for(int ring=0;ring<2;ring++)for(int i=0;i<numbers[ring];i++){double angle=(90+360.0*i/numbers[ring])*Math.PI/180;p.Bars.Add(new Rebar(radii[ring]*Math.Cos(angle),radii[ring]*Math.Sin(angle),506.7));}
            // The handbook's COMBINED force table. Its moment values are already enlarged.
            p.Loads.Add(new Demand{Name="편람 LC1 축력최대",P=12948.050,Mx=380.300,My=0});
            p.Loads.Add(new Demand{Name="편람 LC2 모멘트",P=9448.810,Mx=1620.356,My=0});
            p.Loads.Add(new Demand{Name="편람 LC3 탄성",P=7267.450,Mx=9071.833,My=0});
            p.Loads.Add(new Demand{Name="편람 LC4 편심",P=7268.310,Mx=1872.762,My=0});return p;
        }
        public static Project CircularReference()
        {
            const double psi=0.006894757293168;Project p=new Project{Name="공개 검증 · 원형 8-#10 / ACI 318-19",ReductionProfile="Reference-ACI31819-Spiral",BarDisplacement="CenterLumped",CircularRadius=254,AngleCount=144,SliceP=500*4.4482216152605};
            p.Material=new Material{Fc=5000*psi,Fy=60000*psi,Es=29000000*psi,EpsCu=.003,Alpha=.85,Beta=.8};p.Contours.Add(CircleContour(254,false));
            double radius=(10-1.5-.5-1.27/2)*25.4;
            for(int i=0;i<8;i++){double a=(90+i*45)*Math.PI/180;p.Bars.Add(new Rebar(radius*Math.Cos(a),radius*Math.Sin(a),1.27*645.16));}
            p.Loads.Add(new Demand{Name="SP-원형 검토",P=500*4.4482216152605,Mx=250*1.3558179483314});return p;
        }
        public static Project BiaxialReference()
        {
            const double psi=.006894757293168;Project p=new Project{Name="공개 검증 · 16×20 in 이축 RC",ReductionProfile="Reference-ACI31819-Tied",BarDisplacement="CenterLumped",AngleCount=144,SliceP=426*4.4482216152605};
            p.Material=new Material{Fc=6000*psi,Fy=60000*psi,Es=29000000*psi,EpsCu=.003,Alpha=.85,Beta=.75};p.Contours.Add(Polygon(false,-8*25.4,-10*25.4,8*25.4,-10*25.4,8*25.4,10*25.4,-8*25.4,10*25.4));
            foreach(double x in new[]{-5.5,5.5})foreach(double y in new[]{-7.5,-2.5,2.5,7.5})p.Bars.Add(new Rebar(x*25.4,y*25.4,.79*645.16));
            foreach(double y in new[]{-7.5,7.5})p.Bars.Add(new Rebar(0,y*25.4,.79*645.16));
            p.Loads.Add(new Demand{Name="SP-P426 / Mx:My=1.60",P=426*4.4482216152605,Mx=321*1.3558179483314,My=321/1.6*1.3558179483314});return p;
        }
        public static Contour Polygon(bool hole,params double[] xy)
        {
            Contour c=new Contour{Hole=hole};for(int i=0;i<xy.Length;i+=2)c.Points.Add(new Point2(xy[i],xy[i+1]));return c;
        }
    }
}
