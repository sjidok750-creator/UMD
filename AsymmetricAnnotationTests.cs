using System;using System.Drawing;using System.IO;using System.Linq;using PMStudio;
static class AsymmetricAnnotationTests
{
 [STAThread]static int Main(string[] args){try{
 int count=0;
 foreach(bool positive in new[]{false,true})
 {
     var p=AdditionalExamples.UnsymmetricalWall();p.Loads.RemoveAt(positive?0:1);var r=Engine.Analyze(p);
     using(var image=new Bitmap(1000,650))using(var g=Graphics.FromImage(image))using(var drawing=new ReportDrawing())
     {
         g.Clear(Color.White);drawing.DrawCase(g,p,r,0,1000,650);var layout=drawing.Layouts[0];var label=layout.Text.FirstOrDefault(t=>t.Kind=="point"&&t.Text.StartsWith("P₀"));
         if(positive){if(label!=null)throw new Exception("negative-My uniform endpoint must not be labelled at M=0 on positive-My chart");}
         else {if(label==null||label.Anchor.X<=layout.Plot.Left+1)throw new Exception("asymmetric uniform endpoint annotation must retain nonzero moment");}
         count++;Console.WriteLine("PASS ANNOTATION "+(positive?"opposite-direction uniform endpoint is omitted":"nonzero uniform moment is preserved"));
     }
 }
 Console.WriteLine("PASS ANNOTATION total: "+count);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
