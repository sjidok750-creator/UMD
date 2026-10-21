using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PMStudio;

static class RenderingTests
{
    static int count;
    static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS RENDER "+message);}
    static int Ink(Bitmap image,int left,int top,int right,int bottom)
    {
        int n=0;for(int y=top;y<bottom;y+=4)for(int x=left;x<right;x+=4){Color c=image.GetPixel(x,y);if(c.G>c.R+8&&c.G>c.B+8)n++;}return n;
    }
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Project p=Examples.Rectangle();Analysis r=Engine.Analyze(p);string original=Files.Json(r);string folder=args[0];Directory.CreateDirectory(folder);
            using(Dashboard d=new Dashboard{Size=new Size(1200,900)})
            {
                d.CreateControl();d.PerformLayout();d.Controls[0].PerformLayout();d.Bind(p,r);
                foreach(Plot plot in new[]{d.Curve,d.Section,d.Strain,d.Surface})Check(plot.Dock==DockStyle.Fill,"plot fills its panel: "+plot.Mode);
                Check(d.Curve.Width>400&&d.Curve.Height>300&&d.Surface.Width>400,"dashboard charts have usable viewport");Check(d.Strain.Selected!=null&&Math.Abs(d.Strain.Selected.MinSteelStrain+0.002)<1e-8,"initial balanced strain diagram");
                string png=Path.Combine(folder,"interaction-preview.png");d.SaveImage(png,1800,1350);
                using(Bitmap image=new Bitmap(png)){Check(image.Width==1800&&image.Height==1350,"PNG exact size");Check(Ink(image,40,450,880,1250)>500,"PNG left interaction region visible");Check(Ink(image,920,450,1750,1250)>500,"PNG right filled surface visible");}
                d.Bind(null,null);Check(d.Strain.Project==null&&d.Strain.Selected==null&&d.Surface.Result==null,"clearing results clears all dashboard plots");
            }
            using(Plot plot=new Plot{Project=p,Result=r,Selected=r.Limits.BalancedMx.OrderByDescending(x=>x.Mx).First()})
            {
                foreach(string mode in new[]{"Section","Strain","PMx","PMy","Slice","Surface"})foreach(int dpi in new[]{96,144})
                {plot.Mode=mode;using(Bitmap image=new Bitmap(1024,800)){image.SetResolution(dpi,dpi);using(Graphics g=Graphics.FromImage(image))plot.Render(g,1024,800);}Check(true,mode+" at "+dpi+" DPI");}
                plot.Mode="Surface";using(Bitmap image=new Bitmap(900,750))using(Graphics g=Graphics.FromImage(image))
                {
                    foreach(bool drag in new[]{false,true})
                    {
                        typeof(Plot).GetField("dragging",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(plot,drag);
                        plot.Render(g,900,750);double[] ms=new double[30];for(int i=0;i<ms.Length;i++){plot.Rotation+=3;Stopwatch sw=Stopwatch.StartNew();plot.Render(g,900,750);ms[i]=sw.Elapsed.TotalMilliseconds;}Array.Sort(ms);Console.WriteLine((drag?"DRAG":"FULL")+" frame 900x750 / 30 samples: median_ms="+ms[15].ToString("F3")+" p95_ms="+ms[28].ToString("F3"));
                    }
                }
                plot.ResetCamera();Check(plot.Rotation==35&&plot.Elevation==22&&plot.Zoom==1,"camera reset");
            }
            Check(Files.Json(r)==original,"rendering does not mutate engineering results");Console.WriteLine("PASS RENDER total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
