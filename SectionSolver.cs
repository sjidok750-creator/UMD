using System;
using System.Collections.Generic;

namespace PMStudio
{
    // Per-analysis preparation. No shared mutable cache: independent API jobs remain isolated.
    internal sealed class SectionSolver
    {
        sealed class PreparedContour
        {
            public Point2[] Vertices;
            public double Sign;
            public RegionIntegral Full;
        }
        sealed class ProjectedContour
        {
            public PreparedContour Shape;
            public double[] Q;
            public double Min,Max;
        }
        internal sealed class Direction
        {
            public double Angle,Nx,Ny,Max,Span;
            internal double[] BarQ,BarDepth;
            internal object Contours;
        }
        readonly Project p;
        readonly PreparedContour[] shapes;
        readonly Dictionary<double,Direction> directions=new Dictionary<double,Direction>();
        readonly double[] radii;
        readonly RegionIntegral gross;
        readonly double cx,cy;
        public readonly double PMin,PMax;
        public SectionSolver(Project input)
        {
            p=input;shapes=new PreparedContour[p.Contours.Count];gross=new RegionIntegral();
            for(int i=0;i<shapes.Length;i++)
            {
                Contour c=p.Contours[i];RegionIntegral a=Geometry.Integral(c.Points);double sign=c.Hole?-1:1;
                shapes[i]=new PreparedContour{Vertices=c.Points.ToArray(),Sign=sign,Full=a};gross.Area+=sign*a.Area;gross.Qx+=sign*a.Qx;gross.Qy+=sign*a.Qy;
            }
            if(p.CircularRadius>0)gross=Geometry.Region(p,0,1,0,true);
            cx=gross.Qx/gross.Area;cy=gross.Qy/gross.Area;radii=new double[p.Bars.Count];for(int i=0;i<radii.Length;i++)radii[i]=Math.Sqrt(p.Bars[i].Area/Math.PI);
            Direction d=Prepare(0);PMin=Evaluate(d,0,false).P;PMax=Evaluate(d,double.PositiveInfinity,false).P;
        }
        public Direction Prepare(double angle)
        {
            if(!Engine.Finite(angle))throw new ArgumentException("방향이 유한하지 않습니다.");angle%=360;if(angle<0)angle+=360;
            Direction found;if(directions.TryGetValue(angle,out found))return found;
            double t=angle*Math.PI/180;Direction d=new Direction{Angle=angle,Nx=Math.Cos(t),Ny=Math.Sin(t),Max=double.NegativeInfinity};double min=double.PositiveInfinity;
            ProjectedContour[] cs=new ProjectedContour[shapes.Length];
            for(int i=0;i<shapes.Length;i++)
            {
                PreparedContour c=shapes[i];ProjectedContour pc=new ProjectedContour{Shape=c,Q=new double[c.Vertices.Length],Min=double.PositiveInfinity,Max=double.NegativeInfinity};
                for(int j=0;j<c.Vertices.Length;j++){Point2 v=c.Vertices[j];double q=d.Nx*v.X+d.Ny*v.Y;pc.Q[j]=q;pc.Min=Math.Min(pc.Min,q);pc.Max=Math.Max(pc.Max,q);}
                cs[i]=pc;if(c.Sign>0){d.Max=Math.Max(d.Max,pc.Max);min=Math.Min(min,pc.Min);}
            }
            if(p.CircularRadius>0){d.Max=d.Nx*cx+d.Ny*cy+p.CircularRadius;min=d.Max-2*p.CircularRadius;}
            d.Contours=cs;d.Span=d.Max-min;d.BarQ=new double[p.Bars.Count];d.BarDepth=new double[p.Bars.Count];
            for(int i=0;i<p.Bars.Count;i++){Rebar b=p.Bars[i];d.BarQ[i]=d.Nx*b.X+d.Ny*b.Y;d.BarDepth[i]=d.Max-d.BarQ[i];}
            // Bound adaptive angle cache size; each analysis owns and releases its own cache.
            if(directions.Count<768)directions.Add(angle,d);return d;
        }
        public CapacityPoint Evaluate(double angle,double depth,bool details) { return Evaluate(Prepare(angle),depth,details); }
        public CapacityPoint Evaluate(Direction d,double depth,bool details)
        {
            if(double.IsNaN(depth)||double.IsNegativeInfinity(depth)||depth<0)throw new ArgumentException("중립축 깊이가 잘못되었습니다.");
            Material m=p.Material;bool tension=depth==0,full=double.IsPositiveInfinity(depth);double level=full?double.NegativeInfinity:d.Max-m.Beta*depth;
            double ca=0,cqx=0,cqy=0;
            if(!tension)
            {
                if(full){ca=gross.Area;cqx=gross.Qx;cqy=gross.Qy;}
                else if(p.CircularRadius>0){RegionIntegral a=Geometry.CircleSegment(cx,cy,p.CircularRadius,d.Nx,d.Ny,level,false);ca=a.Area;cqx=a.Qx;cqy=a.Qy;}
                else foreach(ProjectedContour c in (ProjectedContour[])d.Contours)
                {
                    if(level>=c.Max)continue;RegionIntegral a=level<=c.Min?c.Shape.Full:Geometry.ProjectedIntegral(c.Shape.Vertices,c.Q,level);ca+=c.Shape.Sign*a.Area;cqx+=c.Shape.Sign*a.Qx;cqy+=c.Shape.Sign*a.Qy;
                }
                for(int i=0;i<p.Bars.Count;i++)
                {
                    Rebar b=p.Bars[i];double radius=radii[i],distance=level-d.BarQ[i],area,first=0;
                    if(!full&&p.BarDisplacement=="CenterLumped"){if(distance>0)continue;area=b.Area;}
                    else if(full||distance<=-radius)area=b.Area;
                    else if(distance>=radius)continue;
                    else{double h=radius*radius-distance*distance;area=radius*radius*Math.Acos(distance/radius)-distance*Math.Sqrt(h);first=2.0/3*Math.Pow(h,1.5);}
                    ca-=area;cqx-=area*b.X+d.Nx*first;cqy-=area*b.Y+d.Ny*first;
                }
            }
            double stress=m.Alpha*m.Fc,force=ca*stress,mx=stress*(cqy-cy*ca),my=-stress*(cqx-cx*ca),sf=0,minE=double.PositiveInfinity,maxE=double.NegativeInfinity;
            CapacityPoint r=new CapacityPoint{Angle=d.Angle,Depth=full?-1:depth,BlockDepth=full?-1:m.Beta*depth,ConcreteArea=ca,ConcreteForce=force/1000,State=tension?"Uniform tension":full?"Uniform compression":"Strain compatibility"};
            if(details)r.Bars=new List<BarState>(p.Bars.Count);
            for(int i=0;i<p.Bars.Count;i++)
            {
                Rebar b=p.Bars[i];double e=tension?-m.Fy/m.Es:full?m.EpsCu:m.EpsCu*(1-d.BarDepth[i]/depth),s=Math.Max(-m.Fy,Math.Min(m.Fy,m.Es*e)),f=s*b.Area;
                sf+=f;mx+=f*(b.Y-cy);my-=f*(b.X-cx);minE=Math.Min(minE,e);maxE=Math.Max(maxE,e);
                if(details)r.Bars.Add(new BarState{X=b.X,Y=b.Y,Area=b.Area,Strain=e,Stress=s,Force=f/1000});
            }
            r.P=(force+sf)/1000;r.Mx=mx/1e6;r.My=my/1e6;r.SteelForce=sf/1000;r.MinSteelStrain=minE;r.MaxSteelStrain=maxE;return r;
        }
        public CapacityPoint AtP(double angle,double axial)
        {
            Direction d=Prepare(angle);double tol=Math.Max(1e-9,Math.Abs(PMax-PMin)*1e-12);
            if(axial<PMin-tol||axial>PMax+tol)return null;
            if(Math.Abs(axial-PMin)<=tol)return Evaluate(d,0,false);if(Math.Abs(axial-PMax)<=tol)return Evaluate(d,double.PositiveInfinity,false);
            double lo=0,hi=1;CapacityPoint r=null;
            for(int i=0;i<64;i++){double u=(lo+hi)/2;r=Evaluate(d,d.Span*u/(1-u),false);if(Math.Abs(r.P-axial)<=tol)return r;if(r.P<axial)lo=u;else hi=u;}
            if(Math.Abs(r.P-axial)>tol*10)throw new ArithmeticException("축력 평형이 수렴하지 않았습니다.");return r;
        }
        public CapacityPoint Balanced(double angle)
        { return AtTensileStrain(angle,p.Material.Fy/p.Material.Es); }
        public CapacityPoint AtTensileStrain(double angle,double tensile)
        {
            Direction d=Prepare(angle);double max=0;foreach(double x in d.BarDepth)max=Math.Max(max,x);
            return Evaluate(d,p.Material.EpsCu/(p.Material.EpsCu+tensile)*max,false);
        }
        public List<CapacityPoint> Slice(double axial,int angles)
        {
            List<CapacityPoint> list=new List<CapacityPoint>(angles);for(int i=0;i<angles;i++){CapacityPoint q=AtP(i*360.0/angles,axial);if(q==null)return list;list.Add(q);}return list;
        }
    }
}
