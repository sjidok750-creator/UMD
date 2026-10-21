using System;
using PMStudio;

namespace PMStudio
{
public static class AdditionalExamples
{
    const double Inch=25.4,Psi=.006894757293168;
    public static Project AciE702()
    {
        var p=new Project{Name="검증 · ACI E702.2 / 12×12 in / 4-#8",BarDisplacement="CenterLumped",AngleCount=144,SliceP=200*4.4482216152605};
        p.Material=new Material{Fc=4000*Psi,Fy=60000*Psi,Es=29000000*Psi,Alpha=.85,Beta=.85,EpsCu=.003};
        p.Contours.Add(Examples.Polygon(false,-6*Inch,-6*Inch,6*Inch,-6*Inch,6*Inch,6*Inch,-6*Inch,6*Inch));
        foreach(double x in new[]{-3.5,3.5})foreach(double y in new[]{-3.5,3.5})p.Bars.Add(new Rebar(x*Inch,y*Inch,.79*Inch*Inch));
        p.Loads.Add(new Demand{Name="E702 공칭 검토",P=200*4.4482216152605,Mx=80*1.3558179483314});return p;
    }
    // Original hand-calculation model in Tables 1/2; NOT the different final spColumn reinforcement.
    // X=-d reproduces Figure 1 and the published negative/positive My convention.
    public static Project UnsymmetricalWall()
    {
        var p=new Project{Name="검증 · SP 비대칭 벽체 / 원문 손계산 배근",BarDisplacement="CenterLumped",AngleCount=144,SliceP=10000*4.4482216152605};
        p.Material=new Material{Fc=4000*Psi,Fy=60000*Psi,Es=29000000*Psi,Alpha=.85,Beta=.85,EpsCu=.003};
        p.Contours.Add(Examples.Polygon(false,0,-70*Inch,0,70*Inch,-14*Inch,70*Inch,-14*Inch,7*Inch,-374*Inch,7*Inch,-374*Inch,12*Inch,-398*Inch,12*Inch,-398*Inch,-12*Inch,-374*Inch,-12*Inch,-374*Inch,-7*Inch,-14*Inch,-7*Inch,-14*Inch,-70*Inch));
        foreach(double d in new[]{2.5,11.5})for(int i=0;i<12;i++)p.Bars.Add(new Rebar(-d*Inch,(-67.5+135.0*i/11)*Inch,.79*Inch*Inch));
        for(int i=0;i<30;i++)foreach(double y in new[]{-4.6875,4.6875})p.Bars.Add(new Rebar(-(20+12*i)*Inch,y*Inch,.31*Inch*Inch));
        foreach(double d in new[]{376.5,395.5})foreach(double y in new[]{-9.5,0,9.5})p.Bars.Add(new Rebar(-d*Inch,y*Inch,.79*Inch*Inch));
        foreach(double y in new[]{-9.5,9.5})p.Bars.Add(new Rebar(-386*Inch,y*Inch,.79*Inch*Inch));
        p.Loads.Add(new Demand{Name="Wall negative My",P=10000*4.4482216152605,My=-95000*1.3558179483314});
        p.Loads.Add(new Demand{Name="Wall positive My",P=6000*4.4482216152605,My=95000*1.3558179483314});return p;
    }
    public static Project HollowBox()
    {
        var p=new Project{Name="독립 적분 검증 · 편심 개구부 상자",BarDisplacement="ExactDisk",AngleCount=144};
        p.Contours.Add(Examples.Polygon(false,-600,-450,600,-450,600,450,-600,450));
        p.Contours.Add(Examples.Polygon(true,-310,-270,390,-270,390,230,-310,230));
        foreach(double x in new[]{-520.0,-260,0,260,520})foreach(double y in new[]{-370.0,370})p.Bars.Add(new Rebar(x,y,794.2));
        foreach(double y in new[]{-185.0,0,185})foreach(double x in new[]{-520.0,520})p.Bars.Add(new Rebar(x,y,794.2));
        p.Loads.Add(new Demand{Name="Box oblique",P=3500,Mx=1800,My=900});return p;
    }
}
}
