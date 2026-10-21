using System;
using System.Collections.Generic;

namespace PMStudio
{
    public sealed class RegionIntegral
    {
        public double Area, Qx, Qy;
    }
    public static class Geometry
    {
        public static double Cross(Point2 a,Point2 b,Point2 c) { return (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X); }
        public static double Distance(Point2 p,Point2 a,Point2 b)
        {
            double dx=b.X-a.X,dy=b.Y-a.Y,d=dx*dx+dy*dy;
            if(d==0)return Math.Sqrt((p.X-a.X)*(p.X-a.X)+(p.Y-a.Y)*(p.Y-a.Y));
            double t=Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/d));
            double x=a.X+t*dx-p.X,y=a.Y+t*dy-p.Y;return Math.Sqrt(x*x+y*y);
        }
        public static bool Inside(Point2 p,List<Point2> v)
        {
            bool inside=false;
            for(int i=0,j=v.Count-1;i<v.Count;j=i++)
            {
                if(Distance(p,v[j],v[i])<1e-7)return true;
                if((v[i].Y>p.Y)!=(v[j].Y>p.Y) && p.X<(v[j].X-v[i].X)*(p.Y-v[i].Y)/(v[j].Y-v[i].Y)+v[i].X)inside=!inside;
            }return inside;
        }
        public static bool Intersects(Point2 a,Point2 b,Point2 c,Point2 d)
        {
            double x=Cross(a,b,c),y=Cross(a,b,d),z=Cross(c,d,a),w=Cross(c,d,b);
            if(((x>0&&y<0)||(x<0&&y>0))&&((z>0&&w<0)||(z<0&&w>0)))return true;
            return Distance(a,c,d)<1e-7||Distance(b,c,d)<1e-7||Distance(c,a,b)<1e-7||Distance(d,a,b)<1e-7;
        }
        public static RegionIntegral Integral(List<Point2> v)
        {
            RegionIntegral r=new RegionIntegral();
            for(int i=0;i<v.Count;i++) { Point2 a=v[i],b=v[(i+1)%v.Count];double k=a.X*b.Y-b.X*a.Y;r.Area+=k;r.Qx+=(a.X+b.X)*k;r.Qy+=(a.Y+b.Y)*k; }
            double sign=r.Area<0?-1:1;r.Area*=sign/2;r.Qx*=sign/6;r.Qy*=sign/6;return r;
        }
        public static List<Point2> Clip(List<Point2> v,double nx,double ny,double level)
        {
            List<Point2> o=new List<Point2>();
            for(int i=0;i<v.Count;i++)
            {
                Point2 a=v[i],b=v[(i+1)%v.Count];double da=nx*a.X+ny*a.Y-level,db=nx*b.X+ny*b.Y-level;
                bool ia=da>=0,ib=db>=0;
                if(ia)o.Add(a);
                if(ia!=ib) { double t=da/(da-db);o.Add(new Point2(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y))); }
            }return o;
        }
        public static RegionIntegral Region(Project p,double nx,double ny,double level,bool full)
        {
            if(p.CircularRadius>0)
            {
                double cx=0,cy=0;foreach(Point2 v in p.Contours[0].Points){cx+=v.X;cy+=v.Y;}cx/=p.Contours[0].Points.Count;cy/=p.Contours[0].Points.Count;
                return CircleSegment(cx,cy,p.CircularRadius,nx,ny,level,full);
            }
            RegionIntegral r=new RegionIntegral();
            foreach(Contour c in p.Contours) { RegionIntegral z=Integral(full?c.Points:Clip(c.Points,nx,ny,level));double s=c.Hole?-1:1;r.Area+=s*z.Area;r.Qx+=s*z.Qx;r.Qy+=s*z.Qy; }
            return r;
        }
        public static RegionIntegral CircleSegment(double cx,double cy,double radius,double nx,double ny,double level,bool full)
        {
            double length=Math.Sqrt(nx*nx+ny*ny);if(length==0)throw new ArgumentException("원형 절단 법선은 0일 수 없습니다.");nx/=length;ny/=length;double d=level/length-nx*cx-ny*cy;
            if(full||d<=-radius){double area=Math.PI*radius*radius;return new RegionIntegral{Area=area,Qx=cx*area,Qy=cy*area};}
            if(d>=radius)return new RegionIntegral();double h=radius*radius-d*d,a=radius*radius*Math.Acos(d/radius)-d*Math.Sqrt(h),q=2.0/3*Math.Pow(h,1.5);
            return new RegionIntegral{Area=a,Qx=cx*a+nx*q,Qy=cy*a+ny*q};
        }
        struct Accumulator
        {
            double fx,fy,px,py,a,qx,qy;
            bool hasPoint;
            public void Add(double x,double y)
            {
                if(!hasPoint){fx=x;fy=y;hasPoint=true;}else{double k=px*y-x*py;a+=k;qx+=(px+x)*k;qy+=(py+y)*k;}px=x;py=y;
            }
            public RegionIntegral Finish()
            {
                if(!hasPoint)return new RegionIntegral();double k=px*fy-fx*py;a+=k;qx+=(px+fx)*k;qy+=(py+fy)*k;double sign=a<0?-1:1;return new RegionIntegral{Area=sign*a/2,Qx=sign*qx/6,Qy=sign*qy/6};
            }
        }
        // Streaming half-plane integration: same clipped boundary, without allocating vertices.
        public static RegionIntegral ProjectedIntegral(Point2[] v,double[] q,double level)
        {
            Accumulator s=new Accumulator();
            for(int i=0;i<v.Length;i++)
            {
                int j=(i+1)%v.Length;double da=q[i]-level,db=q[j]-level;bool ia=da>=0,ib=db>=0;Point2 a=v[i],b=v[j];
                if(ia)s.Add(a.X,a.Y);if(ia!=ib){double t=da/(da-db);s.Add(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y));}
            }return s.Finish();
        }
        // Exact circular steel displacement within the compression half-plane.
        public static RegionIntegral SteelDisk(Rebar b,double nx,double ny,double level,bool full)
        {
            double r=Math.Sqrt(b.Area/Math.PI),d=level-nx*b.X-ny*b.Y;
            if(full||d<=-r)return new RegionIntegral{Area=b.Area,Qx=b.Area*b.X,Qy=b.Area*b.Y};
            if(d>=r)return new RegionIntegral();
            double a=r*r*Math.Acos(d/r)-d*Math.Sqrt(r*r-d*d),q=2.0/3*Math.Pow(r*r-d*d,1.5);
            return new RegionIntegral{Area=a,Qx=a*b.X+nx*q,Qy=a*b.Y+ny*q};
        }
    }
}
