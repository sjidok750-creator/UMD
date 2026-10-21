using System;
using System.Collections.Generic;
using System.Linq;

namespace PMStudio
{
    // Optional editor metadata; the explicit contours and bars remain the analysis input.
    public sealed class SectionRecipe
    {
        public string Shape="Rectangle",Bar="D25",Tie="D10";
        public double Width=600,Height=800,InnerWidth=300,InnerHeight=400,ClearCover=37.77,Area=506.7,Diameter=25.4,RingSpacing=100,StartAngle=0;
        public int Nx=4,Ny=4,Count=16,InnerCount=12;
        public bool SecondRing;
        public SectionRecipe Copy(){return (SectionRecipe)MemberwiseClone();}
    }
    public static class SectionFactory
    {
        public static readonly string[] Shapes={"Rectangle","Circle","HollowCircle","Box","Stadium"};
        public static readonly string[] ShapeNames={"사각형","원형","중공 원형","중공 사각형","장원형"};
        public static string Name(string shape){int i=Array.IndexOf(Shapes,shape);return i<0?"사용자 형상":ShapeNames[i];}
        public static double Area(SectionRecipe r){return r.Bar=="Custom"?r.Area:RebarCatalog.Get(r.Bar).Area;}
        public static double Diameter(SectionRecipe r){return r.Bar=="Custom"?r.Diameter:RebarCatalog.Get(r.Bar).Diameter;}
        public static double Offset(SectionRecipe r){return r.ClearCover+RebarCatalog.Get(r.Tie).Diameter+Diameter(r)/2;}
        static void Require(bool yes,string text){if(!yes)throw new ArgumentException(text);}
        public static Project Build(SectionRecipe r,bool validate)
        {
            Require(r!=null&&Shapes.Contains(r.Shape),"형상을 선택하세요.");
            foreach(double value in new[]{r.Width,r.Height,r.InnerWidth,r.InnerHeight,r.ClearCover,r.Area,r.Diameter,r.RingSpacing,r.StartAngle})Require(Engine.Finite(value),"입력은 유한한 숫자여야 합니다.");
            Require(r.Width>0&&r.Height>0&&r.ClearCover>=0,"단면 치수와 피복을 확인하세요.");
            double offset=Offset(r),area=Area(r);Require(area>0&&Diameter(r)>0,"철근 규격 또는 직접 면적을 확인하세요.");
            var p=new Project{Name="새 RC 단면",SliceP=0,SectionInput=r.Copy()};
            if(r.Shape=="Rectangle"||r.Shape=="Box")
            {
                Require(r.Width>2*offset&&r.Height>2*offset,"피복과 철근 크기에 비해 단면이 작습니다.");
                p.Contours.Add(Examples.Polygon(false,-r.Width/2,-r.Height/2,r.Width/2,-r.Height/2,r.Width/2,r.Height/2,-r.Width/2,r.Height/2));
                if(r.Shape=="Box")
                {Require(r.InnerWidth>0&&r.InnerWidth<r.Width&&r.InnerHeight>0&&r.InnerHeight<r.Height,"내부 치수는 외부 치수보다 작아야 합니다.");p.Contours.Add(Examples.Polygon(true,-r.InnerWidth/2,-r.InnerHeight/2,r.InnerWidth/2,-r.InnerHeight/2,r.InnerWidth/2,r.InnerHeight/2,-r.InnerWidth/2,r.InnerHeight/2));}
                Require(r.Nx>=2&&r.Ny>=2&&r.Nx<=100&&r.Ny<=100&&2*r.Nx+2*r.Ny-4<=300,"각 면 철근 수는 2개 이상, 총 300개 이하입니다.");
                for(int i=0;i<r.Nx;i++){double x=-r.Width/2+offset+(r.Width-2*offset)*i/(r.Nx-1);p.Bars.Add(new Rebar(x,-r.Height/2+offset,area));p.Bars.Add(new Rebar(x,r.Height/2-offset,area));}
                for(int i=1;i<r.Ny-1;i++){double y=-r.Height/2+offset+(r.Height-2*offset)*i/(r.Ny-1);p.Bars.Add(new Rebar(-r.Width/2+offset,y,area));p.Bars.Add(new Rebar(r.Width/2-offset,y,area));}
            }
            else if(r.Shape=="Circle"||r.Shape=="HollowCircle")
            {
                double radius=r.Width/2-offset;Require(radius>0,"피복과 철근 크기에 비해 외경이 작습니다.");
                p.Contours.Add(Examples.CircleContour(r.Width/2,false));if(r.Shape=="Circle")p.CircularRadius=r.Width/2;
                else {Require(r.InnerWidth>0&&r.InnerWidth<r.Width,"내경은 외경보다 작아야 합니다.");p.Contours.Add(Examples.CircleContour(r.InnerWidth/2,true));}
                AddRing(p,radius,r.Count,area,r.StartAngle);
                if(r.SecondRing){Require(r.RingSpacing>0&&r.RingSpacing<radius,"두 링의 중심 간격을 확인하세요.");AddRing(p,radius-r.RingSpacing,r.InnerCount,area,r.StartAngle);}
                Require(p.Bars.Count<=300,"철근은 총 300개 이하입니다.");
            }
            else
            {
                Require(r.Width>=r.Height&&r.Height>2*offset,"장원형은 B≥H이고 피복보다 충분히 커야 합니다.");
                double half=(r.Width-r.Height)/2,outer=r.Height/2,barRadius=outer-offset;
                var contour=new Contour();
                for(int i=0;i<=64;i++){double t=(-90+180.0*i/64)*Math.PI/180;contour.Points.Add(new Point2(half+outer*Math.Cos(t),outer*Math.Sin(t)));}
                for(int i=0;i<=64;i++){double t=(90+180.0*i/64)*Math.PI/180;contour.Points.Add(new Point2(-half+outer*Math.Cos(t),outer*Math.Sin(t)));}
                if(half<1e-8){contour=Examples.CircleContour(outer,false);p.CircularRadius=outer;}
                p.Contours.Add(contour);Require(r.Count>=4&&r.Count<=300,"장원형 철근 수는 4~300개입니다.");
                double line=2*half,arc=Math.PI*barRadius,perimeter=2*(line+arc);
                for(int i=0;i<r.Count;i++)
                {
                    double s=perimeter*i/r.Count,x,y;
                    if(s<line){x=-half+s;y=-barRadius;}
                    else if((s-=line)<arc){double t=-Math.PI/2+s/barRadius;x=half+barRadius*Math.Cos(t);y=barRadius*Math.Sin(t);}
                    else if((s-=arc)<line){x=half-s;y=barRadius;}
                    else {s-=line;double t=Math.PI/2+s/barRadius;x=-half+barRadius*Math.Cos(t);y=barRadius*Math.Sin(t);}
                    p.Bars.Add(new Rebar(x,y,area));
                }
            }
            if(validate)Engine.Validate(p);return p;
        }
        static void AddRing(Project p,double radius,int count,double area,double start)
        {Require(count>=3&&count<=300,"원형 한 링의 철근 수는 3~300개입니다.");for(int i=0;i<count;i++){double t=(start+360.0*i/count)*Math.PI/180;p.Bars.Add(new Rebar(radius*Math.Cos(t),radius*Math.Sin(t),area));}}
        public static bool Matches(Project p,SectionRecipe r)
        {
            try
            {
                Project q=Build(r,false);if(Math.Abs(q.CircularRadius-p.CircularRadius)>1e-5||q.Contours.Count!=p.Contours.Count||q.Bars.Count!=p.Bars.Count)return false;
                for(int i=0;i<p.Contours.Count;i++)
                {Contour a=p.Contours[i],b=q.Contours[i];if(a.Hole!=b.Hole||a.Points.Count!=b.Points.Count)return false;foreach(Point2 v in a.Points)if(!b.Points.Any(w=>Math.Abs(v.X-w.X)<1e-5&&Math.Abs(v.Y-w.Y)<1e-5))return false;}
                var unused=new List<Rebar>(q.Bars);foreach(Rebar b in p.Bars){var found=unused.FirstOrDefault(c=>Math.Abs(b.X-c.X)<1e-5&&Math.Abs(b.Y-c.Y)<1e-5&&Math.Abs(b.Area-c.Area)<1e-6);if(found==null)return false;unused.Remove(found);}return true;
            }catch(ArgumentException){return false;}
        }
        public static SectionRecipe Infer(Project p)
        {
            if(p.SectionInput!=null&&Matches(p,p.SectionInput))return p.SectionInput.Copy();
            if(p.Bars.Count==0||p.Contours.Count>2||p.Bars.Any(b=>Math.Abs(b.Area-p.Bars[0].Area)>1e-6))return null;
            var outer=p.Contours.First(c=>!c.Hole);double minx=outer.Points.Min(v=>v.X),maxx=outer.Points.Max(v=>v.X),miny=outer.Points.Min(v=>v.Y),maxy=outer.Points.Max(v=>v.Y);
            if(Math.Abs(minx+maxx)>1e-5||Math.Abs(miny+maxy)>1e-5)return null;
            var r=new SectionRecipe{Width=maxx-minx,Height=maxy-miny,Bar="Custom",Area=p.Bars[0].Area,Diameter=2*Math.Sqrt(p.Bars[0].Area/Math.PI)};
            var size=RebarCatalog.Sizes.FirstOrDefault(s=>Math.Abs(s.Area-r.Area)<1e-6);if(size!=null){r.Bar=size.Name;r.Diameter=size.Diameter;}
            double extra=RebarCatalog.Get(r.Tie).Diameter+r.Diameter/2;
            if(outer.Points.Count==4)
            {
                if(!outer.Points.All(v=>Math.Abs(Math.Abs(v.X)-r.Width/2)<1e-6&&Math.Abs(Math.Abs(v.Y)-r.Height/2)<1e-6))return null;
                r.Shape=p.Contours.Count==1?"Rectangle":"Box";double c=r.Height/2-p.Bars.Max(b=>b.Y);r.ClearCover=c-extra;
                r.Nx=p.Bars.Count(b=>Math.Abs(b.Y-(r.Height/2-c))<1e-5);r.Ny=p.Bars.Count(b=>Math.Abs(b.X-(r.Width/2-c))<1e-5);
                if(r.Shape=="Box"){var h=p.Contours.First(t=>t.Hole);r.InnerWidth=h.Points.Max(v=>v.X)-h.Points.Min(v=>v.X);r.InnerHeight=h.Points.Max(v=>v.Y)-h.Points.Min(v=>v.Y);}
            }
            else
            {
                if(outer.Points.Count!=128||Math.Abs(r.Width-r.Height)>1e-6||outer.Points.Any(v=>Math.Abs(Math.Sqrt(v.X*v.X+v.Y*v.Y)-r.Width/2)>1e-5))return null;
                r.Shape=p.Contours.Count==1?"Circle":"HollowCircle";if(r.Shape=="Circle"&&p.CircularRadius<=0)return null;
                if(r.Shape=="HollowCircle"){var h=p.Contours.First(t=>t.Hole);r.InnerWidth=2*h.Points.Max(v=>v.X);}
                var radii=p.Bars.Select(b=>Math.Sqrt(b.X*b.X+b.Y*b.Y)).OrderByDescending(x=>x).ToList();double radius=radii[0];var outerBars=p.Bars.Where(b=>Math.Abs(Math.Sqrt(b.X*b.X+b.Y*b.Y)-radius)<1e-5).ToList();r.Count=outerBars.Count;r.ClearCover=r.Width/2-radius-extra;
                r.StartAngle=Math.Atan2(outerBars[0].Y,outerBars[0].X)*180/Math.PI;
                if(radii.Any(v=>Math.Abs(v-radius)>1e-5)){double inner=radii.Last();if(radii.Any(v=>Math.Abs(v-radius)>1e-5&&Math.Abs(v-inner)>1e-5))return null;r.SecondRing=true;r.InnerCount=p.Bars.Count-r.Count;r.RingSpacing=radius-inner;}
            }
            return r.ClearCover>=0&&Matches(p,r)?r:null;
        }
    }
}
