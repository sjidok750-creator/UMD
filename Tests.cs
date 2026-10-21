using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PMStudio;

static class Tests
{
    static int count;
    static void Yes(bool ok,string text) { if(!ok)throw new Exception("FAIL: "+text);count++;Console.WriteLine("PASS "+text); }
    static void Near(double a,double b,double tolerance,string text) { Yes(Math.Abs(a-b)<=tolerance,text+" ["+a.ToString("G12",CultureInfo.InvariantCulture)+" vs "+b.ToString("G12",CultureInfo.InvariantCulture)+"]"); }
    static void Bad(Action action,string text) { try{action();}catch(ArgumentException){Yes(true,text);return;}throw new Exception("Accepted invalid input: "+text); }
    static int Main(string[] args)
    {
        try
        {
            Project p=Examples.Rectangle();Engine.Validate(p);Yes(true,"rectangle valid");
            double area=600*800,sa=p.Bars.Sum(b=>b.Area);
            CapacityPoint comp=Engine.Evaluate(p,90,double.PositiveInfinity,true),tens=Engine.Evaluate(p,90,0,true);
            Near(comp.P,(0.85*30*(area-sa)+400*sa)/1000,1e-8,"uniform compression closed form");Near(tens.P,-400*sa/1000,1e-9,"uniform tension closed form");Near(comp.Mx,0,1e-8,"symmetric compression Mx");Near(comp.My,0,1e-8,"symmetric compression My");
            double c=444,a=0.85*c,cc=0.85*30*600*a,pm=cc,mm=cc*(400-a/2);
            foreach(Rebar b in p.Bars)
            {
                double fs=Math.Max(-400,Math.Min(400,200000*0.003*(1-(400-b.Y)/c)));
                double net=b.Area*(fs-(b.Y>400-a?25.5:0));pm+=net;mm+=net*b.Y;
            }
            CapacityPoint q=Engine.Evaluate(p,90,c,true);Near(q.P,pm/1000,1e-8,"balanced-depth force from independent rectangle formula");Near(q.Mx,mm/1e6,1e-8,"balanced-depth moment from rectangle formula");
            double cb=740*0.003/(0.003+400.0/200000);Near(cb,c,1e-10,"balanced depth");Near(q.MinSteelStrain,-0.002,1e-11,"extreme tensile steel yield at balanced state");
            CapacityPoint pos=Engine.Evaluate(p,90,230,false),neg=Engine.Evaluate(p,270,230,false);Near(pos.P,neg.P,1e-7,"opposite symmetry P");Near(pos.Mx,-neg.Mx,1e-7,"opposite symmetry Mx");
            Project shifted=Files.Clone(p);foreach(Contour co in shifted.Contours)foreach(Point2 v in co.Points){v.X+=1200;v.Y-=780;}foreach(Rebar b in shifted.Bars){b.X+=1200;b.Y-=780;}
            CapacityPoint trans=Engine.Evaluate(shifted,37,500,false),basePt=Engine.Evaluate(p,37,500,false);Near(trans.P,basePt.P,1e-7,"translation P");Near(trans.Mx,basePt.Mx,1e-7,"translation Mx about centroid");Near(trans.My,basePt.My,1e-7,"translation My about centroid");
            Project rotated=Files.Clone(p);double angle=0.37,cs=Math.Cos(angle),sn=Math.Sin(angle);
            foreach(Contour co in rotated.Contours)foreach(Point2 v in co.Points){double x=v.X;v.X=cs*x-sn*v.Y;v.Y=sn*x+cs*v.Y;}foreach(Rebar b in rotated.Bars){double x=b.X;b.X=cs*x-sn*b.Y;b.Y=sn*x+cs*b.Y;}
            CapacityPoint rot=Engine.Evaluate(rotated,37+angle*180/Math.PI,500,false);Near(rot.P,basePt.P,1e-7,"rotation P");Near(rot.Mx,cs*basePt.Mx-sn*basePt.My,1e-7,"rotation Mx covariance");Near(rot.My,sn*basePt.Mx+cs*basePt.My,1e-7,"rotation My covariance");
            Contour u=Examples.Polygon(false,-3,-3,3,-3,3,3,1,3,1,-1,-1,-1,-1,3,-3,3);RegionIntegral split=Geometry.Integral(Geometry.Clip(u.Points,0,1,0));Near(split.Area,12,1e-12,"nonconvex disconnected compression area");Near(split.Qy,18,1e-12,"nonconvex disconnected first moment");
            Rebar disk=new Rebar(0,0,Math.PI*100);RegionIntegral half=Geometry.SteelDisk(disk,0,1,0,false);Near(half.Area,50*Math.PI,1e-10,"half rebar disk area");Near(half.Qy,2000.0/3,1e-10,"half rebar disk first moment");
            RegionIntegral cap=Geometry.SteelDisk(disk,0,1,3,false);double ia=0,iq=0,dy=7.0/200000;for(int i=0;i<200000;i++){double y=3+(i+0.5)*dy,w=2*Math.Sqrt(100-y*y);ia+=w*dy;iq+=w*y*dy;}Near(cap.Area,ia,0.00001,"partial disk independent strip integral area");Near(cap.Qy,iq,0.0001,"partial disk independent strip integral moment");
            foreach(double direction in new[]{0.0,27,90,157,270})foreach(double axial in new[]{-1000.0,0,1500,6000}){CapacityPoint cut=Engine.AtP(p,direction,axial);Yes(cut!=null,"fixed-P solution exists");Near(cut.P,axial,0.00001,"fixed-P equilibrium");}
            Project hollow=Examples.Circle(true);Engine.Validate(hollow);double factor=128*Math.Sin(2*Math.PI/128)/2;Near(Geometry.Region(hollow,0,1,0,true).Area,factor*(500*500-280*280),1e-6,"hollow polygon area");Yes(Engine.AtP(hollow,63,2000)!=null,"hollow biaxial root");
            Project rev=Files.Clone(p);rev.Contours[0].Points.Reverse();Near(Engine.Evaluate(rev,55,300,false).P,Engine.Evaluate(p,55,300,false).P,1e-7,"contour orientation invariant");
            Project bad=Files.Clone(p);bad.Material.Fc=double.NaN;Bad(()=>Engine.Validate(bad),"reject NaN");
            bad=Files.Clone(p);bad.Bars[0].X=1000;Bad(()=>Engine.Validate(bad),"reject outside bar");
            bad=Files.Clone(p);bad.Bars.Add(new Rebar(bad.Bars[0].X,bad.Bars[0].Y,100));Bad(()=>Engine.Validate(bad),"reject overlapping bars");
            bad=Files.Clone(p);bad.Contours.Add(Examples.Polygon(true,1000,1000,1100,1000,1100,1100,1000,1100));Bad(()=>Engine.Validate(bad),"reject outside hole");
            bad=Files.Clone(p);bad.Contours[0]=Examples.Polygon(false,-300,-400,300,400,-300,400,300,-400);Bad(()=>Engine.Validate(bad),"reject self intersection");
            bad=Files.Clone(p);bad.Loads.Add(new Demand{Name=p.Loads[0].Name});Bad(()=>Engine.Validate(bad),"reject duplicate load ID");
            bad=Files.Clone(p);bad.Units="m-N";Bad(()=>Engine.Validate(bad),"reject wrong units");
            bad=Files.Clone(p);bad.Model="KDS";Bad(()=>Engine.Validate(bad),"reject unsupported model");
            Near(Files.Parse(Files.Json(p)).Bars[0].Area,p.Bars[0].Area,1e-10,"JSON roundtrip");
            Stopwatch watch=Stopwatch.StartNew();Analysis r=Engine.Analyze(p);watch.Stop();Yes(r.Curves.Count==72&&r.Slice.Count==144,"surface and refined slice resolution");Yes(r.Curves.SelectMany(x=>x).All(x=>Engine.Finite(x.P)&&Engine.Finite(x.Mx)&&Engine.Finite(x.My)),"all surface points finite");
            Yes(r.Demands[0].Status=="InsideNominal","example interior demand");Yes(r.Demands[2].Status=="OutsideNominal","example exterior demand");
            var zero=Engine.Check(new Demand{Name="zero",P=1500},r.Slice);Yes(zero.Status=="InsideNominal"&&!zero.Utilization.HasValue,"zero moment has no fabricated utilization");
            Yes(Engine.Check(new Demand{Name="out",P=r.PMax+100},Engine.Slice(p,r.PMax+100,72)).Status=="OutsideNominal","out of axial range");
            Yes(r.PMxPaths.SelectMany(x=>x).All(x=>Math.Abs(x.My)<1e-5),"P-Mx path enforces My=0");Yes(r.PMyPaths.SelectMany(x=>x).All(x=>Math.Abs(x.Mx)<1e-5),"P-My path enforces Mx=0");
            CapacityPoint balanced=r.Limits.BalancedMx.OrderByDescending(x=>x.Mx).First();Near(balanced.Depth,444,1e-7,"balanced marker depth");Near(balanced.P,pm/1000,1e-7,"balanced marker axial force");Near(balanced.Mx,mm/1e6,1e-7,"balanced marker moment");Near(balanced.MinSteelStrain,-0.002,1e-10,"balanced marker simultaneous tensile yield");
            Yes(Engine.StrainState(balanced,p.Material)=="균형 변형률","balanced strain classification");Yes(Engine.StrainState(Engine.Evaluate(p,90,600,false),p.Material)=="인장철근 항복 전","before-yield strain classification");Yes(Engine.StrainState(Engine.Evaluate(p,90,200,false),p.Material)=="인장철근 항복 후","after-yield strain classification");Yes(Engine.StrainState(tens,p.Material)!="균형 변형률","uniform tension is not balanced strain");
            Yes(r.DisplayRings.Count>=51&&r.DisplayRings.All(x=>x.Count==p.AngleCount),"equal-P display rings prepared");Yes(r.Limits.PureBendingMx.All(x=>Math.Abs(x.P)<1e-5&&Math.Abs(x.My)<1e-5),"pure bending markers satisfy both equations");
            var end=Engine.Check(new Demand{Name="end",P=r.PMax,Mx=1},Engine.Slice(p,r.PMax,72));Yes(end.Status=="OutsideNominal"&&!end.Utilization.HasValue,"compression endpoint zero moment capacity");
            Project asym=Files.Clone(p);asym.Bars.RemoveAt(0);List<CapacityPoint> asymCut=Engine.Slice(asym,3000,72);var ax=Engine.AxisIntersections(asym,3000,asymCut,true);Yes(ax.Count>=2,"asymmetric axis intersections");Yes(ax.All(x=>Math.Abs(x.My)<1e-5&&Math.Abs(x.P-3000)<1e-4),"asymmetric two-equation equilibrium");
            Analysis asymResult=Engine.Analyze(asym);Yes(asymResult.Limits.BalancedMx.Count==2&&asymResult.Limits.BalancedMx.All(x=>Math.Abs(x.My)<1e-5&&Math.Abs(x.MinSteelStrain+0.002)<1e-8),"asymmetric balanced markers satisfy moment and strain constraints");
            CapacityPoint locked=Engine.Evaluate(asym,90,300,false);Yes(Math.Abs(locked.My)>1,"fixed neutral-axis angle would give wrong opposite moment");
            TriangleReference();
            List<CapacityPoint> diamond=new List<CapacityPoint>{new CapacityPoint{Mx=10,My=0},new CapacityPoint{Mx=0,My=10},new CapacityPoint{Mx=-10,My=0},new CapacityPoint{Mx=0,My=-10}};
            Near(Engine.Check(new Demand{Name="ray",Mx=2,My=3},diamond).Utilization.Value,0.5,1e-12,"ray intersection independent diamond");
            var crossed=new List<CapacityPoint>{new CapacityPoint{Mx=-10,My=-10},new CapacityPoint{Mx=10,My=10},new CapacityPoint{Mx=-10,My=10},new CapacityPoint{Mx=10,My=-10}};Yes(Engine.Check(new Demand{Name="cross",Mx=2,My=3},crossed).Status=="Unresolved","reject self-crossing capacity boundary");
            string folder=args.Length>0?args[0]:Path.GetTempPath();Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"rectangle.pmproj.json"),Files.Json(p),new UTF8Encoding(false));File.WriteAllText(Path.Combine(folder,"hollow-circle.pmproj.json"),Files.Json(hollow),new UTF8Encoding(false));
            Reports.Csv(Path.Combine(folder,"example-surface.csv"),p,r);Reports.Html(Path.Combine(folder,"example-result.html"),p,r);
            Project escaped=Files.Clone(p);escaped.Name="<script>alert(1)</script>";escaped.Loads[0].Name="=1+1";r.Demands[0].Name="=1+1";string html=Path.Combine(folder,"escape-test.html"),csv=Path.Combine(folder,"escape-test.csv");Reports.Html(html,escaped,r);Reports.Csv(csv,escaped,r);Yes(!File.ReadAllText(html).Contains("<script>"),"HTML escapes project name");Yes(File.ReadAllText(csv).Contains("\"'=1+1\""),"CSV formula quoting");
            ApiTest(p,r);
            Console.WriteLine("Engine example elapsed: "+watch.Elapsed.TotalSeconds.ToString("F3",CultureInfo.InvariantCulture)+" s");Console.WriteLine("PASS total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void ApiTest(Project p,Analysis expected)
    {
        TcpListener reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using(ApiServer server=new ApiServer(port))
        {
            server.Start();string host="http://127.0.0.1:"+port;
            using(WebClient client=new WebClient())
            {
                string health=client.DownloadString(host+"/v1/health");Yes(health.Contains("ready"),"live HTTP health");
                client.Encoding=Encoding.UTF8;Project sample=Files.Parse(client.DownloadString(host+"/v1/examples/rectangle"));Near(sample.Bars[0].Area,p.Bars[0].Area,1e-10,"live HTTP example project");
                client.Headers["Content-Type"]="application/json";string json=client.UploadString(host+"/v1/analyze",Files.Json(p));Analysis actual=Files.Serializer().Deserialize<Analysis>(json);Near(actual.PMax,expected.PMax,1e-9,"HTTP vs library compression");Near(actual.Demands[0].Utilization.Value,expected.Demands[0].Utilization.Value,1e-10,"HTTP vs library demand");
            }
            Yes(Raw(port,"POST /v1/analyze HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}").StartsWith("HTTP/1.1 400"),"HTTP invalid project 400");
            Yes(Raw(port,"GET /v1/health HTTP/1.1\r\nHost: evil.example\r\n\r\n").StartsWith("HTTP/1.1 403"),"HTTP rejects non-loopback Host");
            Yes(Raw(port,"GET /v1/health HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nOrigin: https://example.com\r\n\r\n").StartsWith("HTTP/1.1 403"),"HTTP rejects browser origin");
            Yes(Raw(port,"POST /v1/analyze HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nContent-Type: application/json\r\nContent-Length: 2000001\r\n\r\n").StartsWith("HTTP/1.1 413"),"HTTP rejects oversize body");
        }
    }
    static void TriangleReference()
    {
        Project t=new Project();t.Contours.Add(Examples.Polygon(false,-300,-300,300,-300,0,500));t.Bars.Add(new Rebar(-100,-200,400));t.Bars.Add(new Rebar(100,-200,400));t.Bars.Add(new Rebar(0,300,400));Engine.Validate(t);
        double theta=35*Math.PI/180,nx=Math.Cos(theta),ny=Math.Sin(theta),depth=400;
        double qmax=Math.Max(Math.Max(nx*(-300)+ny*(-300),nx*300+ny*(-300)),ny*500),level=qmax-0.85*depth;
        // Independent midpoint-area integration, without polygon clipping or circular-cap formulas.
        double area=0,qx=0,qy=0,dx=0.6,dy=0.8;
        for(int j=0;j<1000;j++)for(int i=0;i<1000;i++)
        {
            double x=-300+(i+0.5)*dx,y=-300+(j+0.5)*dy;if(Math.Abs(x)>300*(500-y)/800||nx*x+ny*y<level)continue;
            bool inBar=false;foreach(Rebar b in t.Bars)if((x-b.X)*(x-b.X)+(y-b.Y)*(y-b.Y)<b.Area/Math.PI){inBar=true;break;}if(inBar)continue;
            area+=dx*dy;qx+=x*dx*dy;qy+=y*dx*dy;
        }
        double centroidY=-100.0/3,force=25.5*area,mx=25.5*(qy-centroidY*area),my=-25.5*qx;
        foreach(Rebar b in t.Bars){double eps=0.003*(1-(qmax-nx*b.X-ny*b.Y)/depth),f=b.Area*Math.Max(-400,Math.Min(400,200000*eps));force+=f;mx+=f*(b.Y-centroidY);my-=f*b.X;}
        CapacityPoint v=Engine.Evaluate(t,35,depth,false);Near(v.P,force/1000,2,"triangle independent 1M-cell force integral");Near(v.Mx,mx/1e6,0.5,"triangle independent 1M-cell Mx integral");Near(v.My,my/1e6,0.5,"triangle independent 1M-cell My integral");
    }
    static string Raw(int port,string request)
    {
        using(TcpClient c=new TcpClient("127.0.0.1",port)){c.ReceiveTimeout=10000;using(NetworkStream s=c.GetStream()){byte[] data=Encoding.ASCII.GetBytes(request);s.Write(data,0,data.Length);using(StreamReader reader=new StreamReader(s,Encoding.UTF8))return reader.ReadToEnd();}}
    }
}
