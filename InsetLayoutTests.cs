using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using PMStudio;

static class InsetLayoutTests
{
    static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Pass(string text){count++;Console.WriteLine("PASS INSET "+text);}
    static void Validate(PlotLayout layout,string id)
    {
        Check(layout.Plot.Contains(layout.Legend)&&layout.Plot.Contains(layout.Values),id+" inset outside plot");
        Check(!layout.Legend.IntersectsWith(layout.Values),id+" insets overlap");
        foreach(PlotAnnotation text in layout.Text)
        {
            RectangleF container=text.Kind=="legend"?layout.Legend:text.Kind=="value"?layout.Values:layout.Plot;
            container.Inflate(-2,-2);Check(container.Contains(text.Bounds),id+" text outside padded area: "+text.Text);
            if(text.Kind=="point")Check(!layout.Legend.IntersectsWith(text.Bounds)&&!layout.Values.IntersectsWith(text.Bounds),id+" point label over inset");
        }
        for(int a=0;a<layout.Text.Count;a++)for(int b=a+1;b<layout.Text.Count;b++)Check(!layout.Text[a].Bounds.IntersectsWith(layout.Text[b].Bounds),id+" text overlap: "+layout.Text[a].Text+" / "+layout.Text[b].Text);
        foreach(PointF[] path in layout.Paths)for(int i=1;i<path.Length;i++)
        {
            Check(!ReportDrawing.SegmentIntersects(path[i-1],path[i],layout.Legend)&&!ReportDrawing.SegmentIntersects(path[i-1],path[i],layout.Values),id+" curve covered by inset");
            foreach(var text in layout.Text.Where(t=>t.Kind=="point"))Check(!ReportDrawing.SegmentIntersects(path[i-1],path[i],text.Bounds),id+" curve crosses point label "+text.Text);
        }
        Check(layout.Text.Any(t=>t.Kind=="point"&&t.Text.StartsWith("D")),id+" demand label missing");
        Check(layout.Text.Count(t=>t.Kind=="value")==6,id+" essential values missing");
    }
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string folder=args[0];Project[] projects={Examples.CircularReference(),Examples.AciReference(false),Examples.BiaxialReference(),Examples.Manual2008(),Examples.Rectangle(),Examples.Pier()};
            string[] names={"circular","rectangular","biaxial","manual","nominal","pier"};
            for(int p=0;p<projects.Length;p++)
            {
                Analysis result=Engine.Analyze(projects[p]);string before=Files.Json(result);
                for(int c=0;c<3;c++)foreach(Size size in new[]{new Size(810,520),new Size(1000,650)})foreach(int dpi in new[]{96,192})
                {
                    string id=names[p]+" case="+c+" "+size+" dpi="+dpi;
                    using(Bitmap image=new Bitmap(size.Width*dpi/96,size.Height*dpi/96))using(Graphics g=Graphics.FromImage(image))using(ReportDrawing drawing=new ReportDrawing())
                    {
                        image.SetResolution(dpi,dpi);g.Clear(Color.White);g.ScaleTransform(dpi/96f,dpi/96f);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                        drawing.DrawCase(g,projects[p],result,c,size.Width,size.Height);Check(drawing.Layouts.Count==1,id+" chart missing");Validate(drawing.Layouts[0],id);
                        if(c==0&&size.Width==1000&&dpi==96)image.Save(Path.Combine(folder,"inset-"+names[p]+"-preview.png"));
                    }
                    Pass(id);
                }
                using(ReportDrawing drawing=new ReportDrawing())using(Bitmap image=drawing.Create(projects[p],result,2400,2700))
                {Check(drawing.Layouts.Count==3,"whole report chart count");foreach(var layout in drawing.Layouts)Validate(layout,names[p]+" whole report");Pass(names[p]+" whole report");}
                Check(before==Files.Json(result),names[p]+" rendering mutated calculation");Pass(names[p]+" rendering retains calculation data");
                if(p==0)using(var sheet=new ReportSheet())
                {
                    sheet.Bind(projects[p],result);
                    for(int c=0;c<3;c++){sheet.CaseSelector.SelectedIndex=c;string label=new[]{"Pmax","Mxmax","Mymax"}[c];sheet.SaveDetailImage(Path.Combine(folder,"report-figure-"+label+".png"));sheet.SaveDetailMetafile(Path.Combine(folder,"report-figure-"+label+".emf"));}
                }
            }
            Console.WriteLine("PASS INSET total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
