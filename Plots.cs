using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PMStudio
{
    public sealed class AxisRange
    {
        public double Min,Max,Step;
        public static AxisRange Nice(double min,double max,int count)
        {
            if(max-min<1e-9){min-=1;max+=1;}double raw=(max-min)/Math.Max(2,count),power=Math.Pow(10,Math.Floor(Math.Log10(raw))),v=raw/power;
            double step=(v<=1?1:v<=2?2:v<=2.5?2.5:v<=5?5:10)*power;return new AxisRange{Min=Math.Floor(min/step)*step,Max=Math.Ceiling(max/step)*step,Step=step};
        }
        public double Unit(double x) { return (x-Min)/(Max-Min); }
        public IEnumerable<double> Ticks() { int n=(int)Math.Round((Max-Min)/Step);for(int i=0;i<=n&&i<30;i++)yield return Min+i*Step; }
        public string Text(double x) { if(Math.Abs(x)<Step*1e-7)x=0;return Math.Abs(x)>=1e6?x.ToString("0.##E+0",CultureInfo.InvariantCulture):x.ToString(Step<1?"0.###":"#,0.##",CultureInfo.InvariantCulture); }
    }
    internal struct V3 { public double X,Y,Z;public V3(double x,double y,double z){X=x;Y=y;Z=z;} }
    internal sealed class MeshFace { public int A,B,C,D,Shade;public double Depth; }
    internal sealed class SurfaceMesh
    {
        public V3[] Vertices;public MeshFace[] Faces;public PointF[] Screen;public double[] Depth;
        public SurfaceMesh(Analysis r,AxisRange ax,AxisRange ay,AxisRange ap,int directionLimit,int depthLimit)
        {
            bool rings=r.DisplayRings!=null&&r.DisplayRings.Count>1;int n=rings?r.DisplayRings[0].Count:r.Curves.Count,m=rings?r.DisplayRings.Count:r.Curves[0].Count,ns=Math.Min(n,directionLimit),ms=Math.Min(m,depthLimit);
            Vertices=new V3[ns*ms];Screen=new PointF[Vertices.Length];Depth=new double[Vertices.Length];
            for(int i=0;i<ns;i++)for(int j=0;j<ms;j++){CapacityPoint p=rings?r.DisplayRings[j*(m-1)/(ms-1)][i*n/ns]:r.Curves[i*n/ns][j*(m-1)/(ms-1)];Vertices[i*ms+j]=new V3(ax.Unit(p.Mx),ay.Unit(p.My),ap.Unit(p.P));}
            Faces=new MeshFace[ns*(ms-1)];int k=0;for(int i=0;i<ns;i++)for(int j=0;j<ms-1;j++)Faces[k++]=new MeshFace{A=i*ms+j,B=((i+1)%ns)*ms+j,C=((i+1)%ns)*ms+j+1,D=i*ms+j+1,Shade=Math.Min(7,j*8/Math.Max(1,ms-1))};
        }
    }
    public sealed class Plot : Control
    {
        public Project Project;public Analysis Result;public CapacityPoint Selected;public string Mode="Section";
        public bool Compact,ShowNominal=true,ShowLoads=true,ShowSlice=true,ShowGrid=true,ShowSurface=true,ShowReferences=true,ProjectLoads;
        public double Rotation=35,Elevation=22,Zoom=1;public Action<CapacityPoint> Pick;
        readonly Font titleFont=Theme.Font(16,FontStyle.Bold),bodyFont=Theme.Font(12,FontStyle.Regular),smallFont=Theme.Font(11,FontStyle.Regular),axisFont=Theme.Font(12,FontStyle.Bold);
        readonly Pen curvePen=new Pen(Theme.Accent,1.8f),meshPen=new Pen(Color.FromArgb(70,44,126,50),0.6f);
        readonly Brush[] faceBrush=new Brush[8];readonly ToolTip tooltip=new ToolTip{InitialDelay=200,AutoPopDelay=12000};readonly Timer frameTimer=new Timer{Interval=16};readonly List<Hit> hits=new List<Hit>();
        Analysis meshSource;Project meshProject;SurfaceMesh mesh,dragMesh;AxisRange mxRange,myRange,pRange;float w,h,dpi=1;Point lastMouse;bool dragging,pendingFrame;string lastTip="";
        struct Hit { public PointF Position;public CapacityPoint Point;public Demand Load; }
        public Plot()
        {
            DoubleBuffered=true;BackColor=Color.White;Font=new Font(Theme.FontName,9);SetStyle(ControlStyles.ResizeRedraw|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);
            for(int i=0;i<8;i++)faceBrush[i]=new SolidBrush(Color.FromArgb(65,55+i*6,148+i*5,52+i*6));frameTimer.Tick+=(s,e)=>{if(pendingFrame){pendingFrame=false;Invalidate();}};
        }
        protected override void Dispose(bool disposing) { if(disposing){frameTimer.Dispose();tooltip.Dispose();titleFont.Dispose();bodyFont.Dispose();smallFont.Dispose();axisFont.Dispose();curvePen.Dispose();meshPen.Dispose();foreach(Brush b in faceBrush)b.Dispose();}base.Dispose(disposing); }
        public void ResetCamera() { Rotation=35;Elevation=22;Zoom=1;Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Mode=="Surface"){lastMouse=e.Location;dragging=true;Capture=true;frameTimer.Start();Cursor=Cursors.SizeAll;} }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);if(dragging){Rotation+=(e.X-lastMouse.X)/dpi*0.55;Elevation=Math.Max(-65,Math.Min(65,Elevation+(e.Y-lastMouse.Y)/dpi*0.40));lastMouse=e.Location;pendingFrame=true;return;}
            Hit? hit=Nearest(e.X/dpi,e.Y/dpi,14);string text="";
            if(hit.HasValue){Hit v=hit.Value;if(v.Load!=null)text=string.Format("{0}\nP {1:N3} kN\nMx {2:N3} / My {3:N3} kN·m",v.Load.Name,v.Load.P,v.Load.Mx,v.Load.My);else text=string.Format("P {0:N3} kN\nMx {1:N3} / My {2:N3} kN·m\nθ {3:0.###}° / c {4}",v.Point.P,v.Point.Mx,v.Point.My,v.Point.Angle,v.Point.Depth<0?"∞":v.Point.Depth.ToString("0.###")+" mm");}
            if(text!=lastTip){lastTip=text;tooltip.SetToolTip(this,text);}
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e);if(dragging){dragging=false;Capture=false;frameTimer.Stop();pendingFrame=false;Cursor=Cursors.Default;Invalidate();return;}Hit? hit=Nearest(e.X/dpi,e.Y/dpi,18);if(hit.HasValue&&hit.Value.Point!=null){Selected=hit.Value.Point;if(Pick!=null)Pick(Selected);Invalidate();} }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;frameTimer.Stop();Cursor=Cursors.Default;} }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e);if(Mode=="Surface"){Zoom=Math.Max(0.65,Math.Min(1.55,Zoom*(e.Delta>0?1.08:1/1.08)));Invalidate();} }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e);if(Mode=="Surface")ResetCamera(); }
        Hit? Nearest(float x,float y,float radius) { Hit? best=null;double distance=radius*radius;foreach(Hit hit in hits){double dx=hit.Position.X-x,dy=hit.Position.Y-y,d=dx*dx+dy*dy;if(d<distance){distance=d;best=hit;}}return best; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e);Render(e.Graphics,Width,Height); }
        public void Render(Graphics g,int width,int height)
        {
            GraphicsState saved=g.Save();try
            {
                g.Clear(Color.White);dpi=g.DpiX/96f;w=width/dpi;h=height/dpi;g.ScaleTransform(dpi,dpi);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;hits.Clear();if(w<100||h<90)return;
                if(Project==null){Heading(g,"단면내력 해석","입력을 적용한 뒤 상관면을 계산하세요.");return;}if(Mode=="Section")Section(g);else if(Mode=="Strain")Strain(g);else if(Result==null)Heading(g,"계산 대기","상관면 계산(F5)을 실행하세요.");else if(Mode=="Surface")Surface(g);else Curve(g);
            }finally{g.Restore(saved);}
        }
        void DrawText(Graphics g,string s,Font f,Brush b,float x,float y) { g.DrawString(s,f,b,x,y); }
        void Center(Graphics g,string s,Font f,Brush b,float x,float y) { SizeF z=g.MeasureString(s,f);DrawText(g,s,f,b,x-z.Width/2,y); }
        void RightText(Graphics g,string s,Font f,Brush b,float x,float y) { SizeF z=g.MeasureString(s,f);DrawText(g,s,f,b,x-z.Width,y); }
        void Heading(Graphics g,string title,string sub) { using(Brush b=new SolidBrush(Theme.Ink))DrawText(g,title,titleFont,b,18,12);using(Brush b=new SolidBrush(Theme.Muted))g.DrawString(sub,smallFont,b,new RectangleF(18,37,w-32,31)); }
        void Section(Graphics g)
        {
            Point2[] v=Project.Contours.SelectMany(c=>c.Points).ToArray();if(v.Length<3)return;double minx=v.Min(p=>p.X),maxx=v.Max(p=>p.X),miny=v.Min(p=>p.Y),maxy=v.Max(p=>p.Y);
            if(!Compact)Heading(g,"단면 및 철근 배치","좌표 mm · "+(Project.CircularRadius>0?"원형 원호 적분":"다각형 적분")+" · 점선은 중립축");else DrawText(g,"RC 단면 / 배근",axisFont,Brushes.DarkSlateGray,15,8);
            double top=Compact?34:76,bottom=h-(Compact?48:67),left=Compact?48:59,right=w-(Compact?38:82),scale=Math.Min((right-left)/Math.Max(1,maxx-minx),(bottom-top)/Math.Max(1,maxy-miny));if(scale<=0)return;
            Func<double,double,PointF> tr=(x,y)=>new PointF((float)((left+right)/2+(x-(minx+maxx)/2)*scale),(float)((top+bottom)/2-(y-(miny+maxy)/2)*scale));
            using(GraphicsPath path=new GraphicsPath(FillMode.Alternate))
            {
                foreach(Contour c in Project.Contours)path.AddPolygon(c.Points.Select(p=>tr(p.X,p.Y)).ToArray());using(Brush b=new SolidBrush(Color.FromArgb(247,187,118)))g.FillPath(b,path);
                if(Selected!=null&&Selected.Depth!=0)
                {
                    GraphicsState old=g.Save();g.SetClip(path);double t=Selected.Angle*Math.PI/180,nx=Math.Cos(t),ny=Math.Sin(t),qmax=Project.CircularRadius>0?nx*v.Average(p=>p.X)+ny*v.Average(p=>p.Y)+Project.CircularRadius:v.Max(p=>nx*p.X+ny*p.Y),level=Selected.Depth<0?-1e20:qmax-Project.Material.Beta*Selected.Depth;
                    var clip=Geometry.Clip(new List<Point2>{new Point2(minx,miny),new Point2(maxx,miny),new Point2(maxx,maxy),new Point2(minx,maxy)},nx,ny,level);if(clip.Count>=3)using(Brush b=new SolidBrush(Color.FromArgb(115,170,78,55)))g.FillPolygon(b,clip.Select(p=>tr(p.X,p.Y)).ToArray());
                    if(Selected.Depth>0){double q=qmax-Selected.Depth,len=Math.Max(maxx-minx,maxy-miny)*3;using(Pen pen=new Pen(Color.FromArgb(65,79,93),1.2f)){pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,tr(nx*q-ny*len,ny*q+nx*len),tr(nx*q+ny*len,ny*q-nx*len));}}g.Restore(old);
                }
                using(Pen pen=new Pen(Color.FromArgb(96,65,34),1.4f))g.DrawPath(pen,path);
            }
            for(int i=0;i<Project.Bars.Count;i++){Rebar b=Project.Bars[i];PointF q=tr(b.X,b.Y);float d=(float)Math.Max(4,2*Math.Sqrt(b.Area/Math.PI)*scale);Brush ink=Brushes.Black;if(Selected!=null&&Selected.Bars!=null&&i<Selected.Bars.Count)ink=Selected.Bars[i].Stress>=0?Brushes.Firebrick:Brushes.RoyalBlue;g.FillEllipse(ink,q.X-d/2,q.Y-d/2,d,d);}
            PointF tl=tr(minx,maxy),br=tr(maxx,miny);float dy=br.Y+16,dx=tl.X-18;
            using(Pen pen=new Pen(Color.FromArgb(87,96,104),0.8f)){g.DrawLine(pen,tl.X,br.Y+4,tl.X,dy+6);g.DrawLine(pen,br.X,br.Y+4,br.X,dy+6);g.DrawLine(pen,tl.X,dy,br.X,dy);Tick(g,pen,tl.X,dy);Tick(g,pen,br.X,dy);g.DrawLine(pen,dx-5,tl.Y,tl.X-4,tl.Y);g.DrawLine(pen,dx-5,br.Y,tl.X-4,br.Y);g.DrawLine(pen,dx,tl.Y,dx,br.Y);Tick(g,pen,dx,tl.Y);Tick(g,pen,dx,br.Y);}
            Center(g,(maxx-minx).ToString("0.##")+" mm",smallFont,Brushes.DarkSlateGray,(tl.X+br.X)/2,dy+4);GraphicsState gs=g.Save();g.TranslateTransform(dx-18,(tl.Y+br.Y)/2);g.RotateTransform(-90);Center(g,(maxy-miny).ToString("0.##")+" mm",smallFont,Brushes.DarkSlateGray,0,-7);g.Restore(gs);
            bool same=Project.Bars.All(b=>Math.Abs(b.Area-Project.Bars[0].Area)<1e-6);string label=Project.Bars.Count+"개"+(same?" × Ø"+(2*Math.Sqrt(Project.Bars[0].Area/Math.PI)).ToString("0.#"):"");if(!Compact){float tx=br.X+12;g.DrawLine(Pens.DimGray,br.X-8,tl.Y+8,tx,tl.Y-2);DrawText(g,label,smallFont,Brushes.DarkSlateGray,tx,tl.Y-13);}
            if(!Compact)DrawText(g,"X → / Y ↑   ·   As = "+Project.Bars.Sum(b=>b.Area).ToString("#,0.##")+" mm²",bodyFont,Brushes.DimGray,18,h-25);else RightText(g,"mm",smallFont,Brushes.DimGray,w-12,h-22);
        }
        static void Tick(Graphics g,Pen pen,float x,float y) { g.DrawLine(pen,x-3,y+3,x+3,y-3); }
        void Strain(Graphics g)
        {
            DrawText(g,"변형률 분포",axisFont,Brushes.DarkSlateGray,12,8);CapacityPoint q=Selected;
            if(q==null){DrawText(g,"균형점 B 또는 곡선의 점을 선택하세요.",smallFont,Brushes.DimGray,12,34);return;}
            double angle=q.Angle*Math.PI/180,nx=Math.Cos(angle),ny=Math.Sin(angle);double[] projections=Project.Contours.Where(c=>!c.Hole).SelectMany(c=>c.Points).Select(v=>nx*v.X+ny*v.Y).ToArray();double support=Project.CircularRadius>0?nx*Project.Contours[0].Points.Average(p=>p.X)+ny*Project.Contours[0].Points.Average(p=>p.Y)+Project.CircularRadius:projections.Max();double span=Project.CircularRadius>0?2*Project.CircularRadius:projections.Max()-projections.Min(),dt=0;
            foreach(Rebar b in Project.Bars)dt=Math.Max(dt,support-nx*b.X-ny*b.Y);
            double top=q.Depth==0?-Project.Material.Fy/Project.Material.Es:Project.Material.EpsCu,bottom=q.Depth==0?top:q.Depth<0?top:top*(1-span/q.Depth),et=Math.Max(0,-q.MinSteelStrain),ey=Project.Material.Fy/Project.Material.Es;
            double min=Math.Min(0,Math.Min(top,bottom)),max=Math.Max(0,Math.Max(top,bottom));if(max-min<1e-10)return;
            float ytop=37,ybottom=h-53;Func<double,float> xx=e=>(float)(38+(e-min)/(max-min)*Math.Max(40,w-116));float x0=xx(0);Func<double,float> yy=d=>(float)(ytop+d/span*(ybottom-ytop));
            using(Pen axis=new Pen(Color.DimGray,0.8f)){axis.DashStyle=DashStyle.Dash;g.DrawLine(axis,x0,ytop-3,x0,ybottom+3);}
            if(top*bottom<0)
            {
                float yn=yy(q.Depth);using(Brush red=new SolidBrush(Color.FromArgb(210,205,89,69)))g.FillPolygon(red,new[]{new PointF(x0,ytop),new PointF(xx(top),ytop),new PointF(x0,yn)});using(Brush blue=new SolidBrush(Color.FromArgb(165,82,133,179)))g.FillPolygon(blue,new[]{new PointF(x0,yn),new PointF(xx(bottom),ybottom),new PointF(x0,ybottom)});
            }
            else using(Brush b=new SolidBrush(top>=0?Color.FromArgb(160,205,89,69):Color.FromArgb(155,82,133,179)))g.FillPolygon(b,new[]{new PointF(x0,ytop),new PointF(xx(top),ytop),new PointF(xx(bottom),ybottom),new PointF(x0,ybottom)});
            g.DrawLine(Pens.DarkSlateGray,xx(top),ytop,xx(bottom),ybottom);
            DrawText(g,"εtop="+top.ToString("0.#####"),smallFont,Brushes.DarkSlateGray,Math.Max(5,Math.Min(w-96,xx(top)+5)),ytop-15);
            using(Pen bar=new Pen(Color.FromArgb(77,97,113),0.8f)){bar.DashStyle=DashStyle.Dot;g.DrawLine(bar,23,yy(dt),w-20,yy(dt));}
            DrawText(g,"εt="+et.ToString("0.#####")+" / εy="+ey.ToString("0.#####"),smallFont,Brushes.DarkSlateGray,12,h-42);
            DrawText(g,Engine.StrainState(q,Project.Material),axisFont,Brushes.DarkSlateGray,12,h-25);
        }
        bool DisplayLoad(Demand d,bool slice,bool xaxis) { return slice?Math.Abs(d.P-Result.SliceP)<=0.001:ProjectLoads||Math.Abs(xaxis?d.My:d.Mx)<=1e-7; }
        void Curve(Graphics g)
        {
            bool isSlice=Mode=="Slice",xaxis=Mode=="PMx";Heading(g,isSlice?"Mx–My 상관곡선":xaxis?"P–Mx 상관도  |  My = 0":"P–My 상관도  |  Mx = 0",isSlice?string.Format("P = {0:#,0.###} kN · 공칭 절단곡선",Result.SliceP):ProjectLoads?"실선: 반대축 M=0 내력 · 빈 기호: 양축하중 투영 (2D 판정 불가)":"반대축 모멘트 0인 조합만 표시 · 양축 판정은 절단면에서 확인");
            List<List<CapacityPoint>> paths=isSlice?new List<List<CapacityPoint>>():xaxis?Result.PMxPaths:Result.PMyPaths;if(isSlice&&Result.Slice.Count>0)paths.Add(Result.Slice.Concat(new[]{Result.Slice[0]}).ToList());if(paths.Count==0){DrawText(g,"해당 축력/축의 평형 경로가 없습니다.",bodyFont,Brushes.DimGray,20,90);return;}
            Func<CapacityPoint,double> fx=p=>isSlice?p.Mx:xaxis?p.Mx:p.My,fy=p=>isSlice?p.My:p.P;var pts=paths.SelectMany(p=>p).ToArray();double minx=Math.Min(0,pts.Min(fx)),maxx=Math.Max(0,pts.Max(fx)),miny=Math.Min(0,pts.Min(fy)),maxy=Math.Max(0,pts.Max(fy));
            if(ShowLoads)foreach(Demand d in Project.Loads)if(DisplayLoad(d,isSlice,xaxis)){double x=isSlice?d.Mx:xaxis?d.Mx:d.My,y=isSlice?d.My:d.P;minx=Math.Min(minx,x);maxx=Math.Max(maxx,x);miny=Math.Min(miny,y);maxy=Math.Max(maxy,y);}
            AxisRange ax=AxisRange.Nice(minx,maxx,w>500?6:4),ay=AxisRange.Nice(miny,maxy,h>420?7:5);RectangleF box=new RectangleF(74,88,Math.Max(30,w-108),Math.Max(30,h-157));Func<double,double,PointF> tr=(x,y)=>new PointF((float)(box.Left+ax.Unit(x)*box.Width),(float)(box.Bottom-ay.Unit(y)*box.Height));
            if(ShowNominal&&!isSlice&&paths.Count==2&&paths[0].Count>1&&paths[1].Count>1)using(Brush fill=new SolidBrush(Color.FromArgb(35,82,166,83))){var polygon=paths[0].Concat(paths[1].AsEnumerable().Reverse()).Select(p=>tr(fx(p),fy(p))).ToArray();g.FillPolygon(fill,polygon);}
            using(Pen grid=new Pen(Color.FromArgb(220,225,229),0.8f)){foreach(double x in ax.Ticks()){float px=tr(x,0).X;if(ShowGrid)g.DrawLine(grid,px,box.Top,px,box.Bottom);Center(g,ax.Text(x),smallFont,Brushes.DimGray,px,box.Bottom+7);}foreach(double y in ay.Ticks()){float py=tr(0,y).Y;if(ShowGrid)g.DrawLine(grid,box.Left,py,box.Right,py);RightText(g,ay.Text(y),smallFont,Brushes.DimGray,box.Left-9,py-7);}}
            using(Pen axis=new Pen(Color.FromArgb(58,67,75),1.15f)){axis.EndCap=LineCap.ArrowAnchor;g.DrawLine(axis,tr(ax.Min,0),tr(ax.Max,0));g.DrawLine(axis,tr(0,ay.Min),tr(0,ay.Max));}
            if(ShowSlice&&!isSlice&&Result.SliceP>=ay.Min&&Result.SliceP<=ay.Max)using(Pen cut=new Pen(Color.FromArgb(143,76,150),1)){cut.DashStyle=DashStyle.Dash;float y=tr(0,Result.SliceP).Y;g.DrawLine(cut,box.Left,y,box.Right,y);RightText(g,"P = "+Result.SliceP.ToString("#,0.##"),smallFont,Brushes.DarkMagenta,box.Right,y-17);}
            if(ShowNominal)foreach(var line in paths){PointF[] screen=line.Select(p=>tr(fx(p),fy(p))).ToArray();if(screen.Length>1)g.DrawLines(curvePen,screen);foreach(CapacityPoint p in line)hits.Add(new Hit{Position=tr(fx(p),fy(p)),Point=p});}
            if(ShowNominal&&ShowReferences&&!isSlice&&Result.Limits!=null)
            {
                var balanced=xaxis?Result.Limits.BalancedMx:Result.Limits.BalancedMy;var pure=xaxis?Result.Limits.PureBendingMx:Result.Limits.PureBendingMy;
                foreach(CapacityPoint p in balanced)
                {
                    PointF b=tr(fx(p),p.P);using(Pen guide=new Pen(Color.FromArgb(108,115,120),0.85f)){guide.DashStyle=DashStyle.Dash;g.DrawLine(guide,tr(0,p.P),b);g.DrawLine(guide,tr(fx(p),0),b);g.DrawLine(guide,tr(0,0),b);}
                    g.FillEllipse(Brushes.White,b.X-4,b.Y-4,8,8);g.DrawEllipse(curvePen,b.X-4,b.Y-4,8,8);DrawText(g,fx(p)>=0?"B+":"B−",axisFont,Brushes.DarkGreen,b.X+(fx(p)>=0?7:-24),b.Y-20);hits.Add(new Hit{Position=b,Point=p});
                    if(fx(p)>0&&Math.Abs(p.P)>1e-5){PointF middle=tr(fx(p)*0.50,p.P*0.50);DrawText(g,"eb="+(1000*fx(p)/p.P).ToString("0.##")+" mm",smallFont,Brushes.DimGray,middle.X+5,middle.Y-13);}
                }
                foreach(CapacityPoint p in pure){PointF b=tr(fx(p),0);g.FillRectangle(Brushes.DarkGreen,b.X-2,b.Y-2,4,4);if(fx(p)>0)DrawText(g,"M₀",smallFont,Brushes.DarkGreen,b.X+5,b.Y+5);hits.Add(new Hit{Position=b,Point=p});}
            }
            if(ShowLoads)for(int i=0;i<Project.Loads.Count;i++){Demand d=Project.Loads[i];if(!DisplayLoad(d,isSlice,xaxis))continue;PointF q=tr(isSlice?d.Mx:xaxis?d.Mx:d.My,isSlice?d.My:d.P);bool projection=!isSlice&&Math.Abs(xaxis?d.My:d.Mx)>1e-7;using(Brush b=new SolidBrush(Theme.LoadColors[i%Theme.LoadColors.Length]))using(Pen pen=new Pen(Theme.LoadColors[i%Theme.LoadColors.Length],1.5f)){if(projection){g.FillEllipse(Brushes.White,q.X-4,q.Y-4,8,8);g.DrawEllipse(pen,q.X-4,q.Y-4,8,8);}else{g.FillRectangle(b,q.X-3.5f,q.Y-3.5f,7,7);g.DrawRectangle(Pens.White,q.X-3.5f,q.Y-3.5f,7,7);}}hits.Add(new Hit{Position=q,Load=d});}
            if(Selected!=null){PointF q=tr(fx(Selected),fy(Selected));g.FillEllipse(Brushes.White,q.X-4,q.Y-4,8,8);g.DrawEllipse(Pens.Firebrick,q.X-5,q.Y-5,10,10);}
            DrawText(g,isSlice?"My (kN·m)":"P (kN)",axisFont,Brushes.DarkSlateGray,18,66);RightText(g,isSlice?"Mx (kN·m)":xaxis?"Mx (kN·m)":"My (kN·m)",axisFont,Brushes.DarkSlateGray,box.Right,h-33);DrawText(g,"공칭내력 · 곡선 점 선택 / 하중점에 마우스를 올려 수치 확인",smallFont,Brushes.DimGray,18,h-18);
        }
        void PrepareMesh()
        {
            if(ReferenceEquals(meshSource,Result)&&ReferenceEquals(meshProject,Project))return;meshSource=Result;meshProject=Project;var all=Result.Curves.SelectMany(c=>c).ToArray();mxRange=AxisRange.Nice(Math.Min(0,all.Min(p=>p.Mx)),Math.Max(0,all.Max(p=>p.Mx)),5);myRange=AxisRange.Nice(Math.Min(0,all.Min(p=>p.My)),Math.Max(0,all.Max(p=>p.My)),5);pRange=AxisRange.Nice(Result.PMin,Result.PMax,7);mesh=new SurfaceMesh(Result,mxRange,myRange,pRange,72,42);dragMesh=new SurfaceMesh(Result,mxRange,myRange,pRange,36,26);
        }
        void Surface(Graphics g)
        {
            if(dragging){g.SmoothingMode=SmoothingMode.None;g.CompositingQuality=CompositingQuality.HighSpeed;g.PixelOffsetMode=PixelOffsetMode.HighSpeed;}
            if(Result.Curves.Count<2||Result.Curves[0].Count<2){Heading(g,"3D 상관면","상관면 데이터가 없습니다.");return;}PrepareMesh();Heading(g,"P–Mx–My 이축 상관면","드래그 회전 · 휠 확대 · 더블클릭 초기화 · 눈금은 실제 단위");
            double rot=Rotation*Math.PI/180,el=Elevation*Math.PI/180,cr=Math.Cos(rot),sr=Math.Sin(rot),ce=Math.Cos(el),se=Math.Sin(el);Func<V3,V3> view=p=>{double x=p.X-0.5,y=p.Y-0.5,z=p.Z-0.5,u=x*cr-y*sr,v=x*sr+y*cr;return new V3(u,z*ce-v*se,v*ce+z*se);};
            V3[] corners=new V3[8];int ci=0;for(int x=0;x<=1;x++)for(int y=0;y<=1;y++)for(int z=0;z<=1;z++)corners[ci++]=view(new V3(x,y,z));double minu=corners.Min(q=>q.X),maxu=corners.Max(q=>q.X),minv=corners.Min(q=>q.Y),maxv=corners.Max(q=>q.Y);
            RectangleF area=new RectangleF(66,79,Math.Max(50,w-132),Math.Max(50,h-166));double scale=Math.Min(area.Width/(maxu-minu),area.Height/(maxv-minv))*Zoom;Func<V3,PointF> project=p=>{V3 q=view(p);return new PointF((float)(area.Left+area.Width/2+(q.X-(minu+maxu)/2)*scale),(float)(area.Top+area.Height/2-(q.Y-(minv+maxv)/2)*scale));};double farx=sr>=0?0:1,fary=cr>=0?0:1,frontx=1-farx,fronty=1-fary;
            if(ShowGrid)using(Brush wall=new SolidBrush(Color.FromArgb(242,244,245)))using(Pen grid=new Pen(Color.FromArgb(196,203,208),0.75f))
            {
                grid.DashStyle=DashStyle.Dot;g.FillPolygon(wall,new[]{project(new V3(0,0,0)),project(new V3(1,0,0)),project(new V3(1,1,0)),project(new V3(0,1,0))});g.FillPolygon(wall,new[]{project(new V3(farx,0,0)),project(new V3(farx,1,0)),project(new V3(farx,1,1)),project(new V3(farx,0,1))});g.FillPolygon(wall,new[]{project(new V3(0,fary,0)),project(new V3(1,fary,0)),project(new V3(1,fary,1)),project(new V3(0,fary,1))});
                foreach(double n in mxRange.Ticks()){double u=mxRange.Unit(n);g.DrawLine(grid,project(new V3(u,0,0)),project(new V3(u,1,0)));g.DrawLine(grid,project(new V3(u,fary,0)),project(new V3(u,fary,1)));}foreach(double n in myRange.Ticks()){double u=myRange.Unit(n);g.DrawLine(grid,project(new V3(0,u,0)),project(new V3(1,u,0)));g.DrawLine(grid,project(new V3(farx,u,0)),project(new V3(farx,u,1)));}foreach(double n in pRange.Ticks()){double u=pRange.Unit(n);g.DrawLine(grid,project(new V3(farx,0,u)),project(new V3(farx,1,u)));g.DrawLine(grid,project(new V3(0,fary,u)),project(new V3(1,fary,u)));}
                grid.DashStyle=DashStyle.Solid;grid.Color=Color.FromArgb(142,153,162);g.DrawPolygon(grid,new[]{project(new V3(0,0,0)),project(new V3(1,0,0)),project(new V3(1,1,0)),project(new V3(0,1,0))});g.DrawLine(grid,project(new V3(farx,0,0)),project(new V3(farx,0,1)));g.DrawLine(grid,project(new V3(farx,1,0)),project(new V3(farx,1,1)));g.DrawLine(grid,project(new V3(1-farx,fary,0)),project(new V3(1-farx,fary,1)));g.DrawLine(grid,project(new V3(0,fary,1)),project(new V3(1,fary,1)));g.DrawLine(grid,project(new V3(farx,0,1)),project(new V3(farx,1,1)));
            }
            if(ShowNominal){SurfaceMesh m=dragging?dragMesh:mesh;for(int i=0;i<m.Vertices.Length;i++){m.Screen[i]=project(m.Vertices[i]);m.Depth[i]=view(m.Vertices[i]).Z;}foreach(MeshFace f in m.Faces)f.Depth=(m.Depth[f.A]+m.Depth[f.B]+m.Depth[f.C]+m.Depth[f.D])/4;Array.Sort(m.Faces,(a,b)=>a.Depth.CompareTo(b.Depth));PointF[] polygon=new PointF[4];foreach(MeshFace f in m.Faces){polygon[0]=m.Screen[f.A];polygon[1]=m.Screen[f.B];polygon[2]=m.Screen[f.C];polygon[3]=m.Screen[f.D];if(ShowSurface)g.FillPolygon(faceBrush[f.Shade],polygon);g.DrawPolygon(meshPen,polygon);}}
            if(ShowSlice&&Result.Slice.Count>1)using(Pen cut=new Pen(Color.FromArgb(146,74,145),1.6f)){var points=Result.Slice.Select(p=>project(new V3(mxRange.Unit(p.Mx),myRange.Unit(p.My),pRange.Unit(p.P)))).ToList();points.Add(points[0]);g.DrawLines(cut,points.ToArray());}
            if(ShowLoads)for(int i=0;i<Project.Loads.Count;i++){Demand d=Project.Loads[i];PointF q=project(new V3(mxRange.Unit(d.Mx),myRange.Unit(d.My),pRange.Unit(d.P)));if(q.X<8||q.Y<65||q.X>w-8||q.Y>h-44)continue;using(Brush b=new SolidBrush(Theme.LoadColors[i%Theme.LoadColors.Length])){g.FillRectangle(b,q.X-3.5f,q.Y-3.5f,7,7);g.DrawRectangle(Pens.White,q.X-3.5f,q.Y-3.5f,7,7);}hits.Add(new Hit{Position=q,Load=d});}
            using(Pen axis=new Pen(Color.FromArgb(81,91,101),1))
            {
                g.DrawLine(axis,project(new V3(0,fronty,0)),project(new V3(1,fronty,0)));g.DrawLine(axis,project(new V3(frontx,0,0)),project(new V3(frontx,1,0)));foreach(double n in mxRange.Ticks()){PointF q=project(new V3(mxRange.Unit(n),fronty,0));Center(g,mxRange.Text(n),smallFont,Brushes.DimGray,q.X,q.Y+6);}foreach(double n in myRange.Ticks()){if(Math.Abs(myRange.Unit(n)-fronty)<1e-9)continue;PointF q=project(new V3(frontx,myRange.Unit(n),0));Center(g,myRange.Text(n),smallFont,Brushes.DimGray,q.X,q.Y+7);}
                PointF lx=project(new V3(0,0,0));double px=0,py=0;for(int x=0;x<=1;x++)for(int y=0;y<=1;y++){PointF q=project(new V3(x,y,0));if(q.X<lx.X){lx=q;px=x;py=y;}}foreach(double n in pRange.Ticks()){PointF q=project(new V3(px,py,pRange.Unit(n)));RightText(g,pRange.Text(n),smallFont,Brushes.DimGray,q.X-9,q.Y-7);}PointF xl=project(new V3(0.5,fronty,0)),yl=project(new V3(frontx,0.5,0)),pl=project(new V3(px,py,1));Center(g,"Mx (kN·m)",axisFont,Brushes.DarkSlateGray,xl.X,xl.Y+31);Center(g,"My (kN·m)",axisFont,Brushes.DarkSlateGray,yl.X,yl.Y+46);RightText(g,"P (kN)",axisFont,Brushes.DarkSlateGray,pl.X-10,pl.Y-27);
            }
            DrawText(g,"축별 표시 배율은 다름 · 표시면 분할은 계산 정밀도와 독립",smallFont,Brushes.DimGray,18,h-19);
        }
    }
    public sealed class Brand : Control
    {
        readonly Font nameFont=Theme.Font(24,FontStyle.Bold),subFont=Theme.Font(12,FontStyle.Regular);
        public Brand(){DoubleBuffered=true;Height=58;BackColor=Color.FromArgb(248,249,252);}
        protected override void Dispose(bool disposing){if(disposing){nameFont.Dispose();subFont.Dispose();}base.Dispose(disposing);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;GraphicsState old=g.Save();float s=g.DpiX/96;g.ScaleTransform(s,s);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;using(Brush b=new SolidBrush(Theme.Accent))g.FillRectangle(b,14,13,38,38);using(Pen p=new Pen(Color.White,3)){g.DrawLine(p,24,32,42,32);g.DrawLine(p,33,23,33,41);}using(Brush b=new SolidBrush(Theme.Ink))g.DrawString("PM Studio +",nameFont,b,65,8);using(Brush b=new SolidBrush(Theme.Muted))g.DrawString("단면내력 해석 / RC / P–M · Biaxial / Local API",subFont,b,68,39);g.DrawLine(Pens.LightGray,0,Height/s-1,Width/s,Height/s-1);g.Restore(old);
        }
    }
}
