using System;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Windows.Forms;using PMStudio;
static class GuidedInputTests
{
 static int count;
 static void Yes(bool value,string text){if(!value)throw new Exception(text);count++;Console.WriteLine("PASS GUIDED "+text);}
 static void Near(double actual,double expected,double tolerance,string text){Yes(Math.Abs(actual-expected)<tolerance,text+" actual="+actual+" expected="+expected);}
 static object Field(MainForm f,string name){return typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
 static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
 static void Set(MainForm f,string name,object value){typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,value);}
 static void Layout(Control c){c.CreateControl();c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
 [STAThread]static int Main(string[] args){try{
 string folder=args[0];var recipe=new SectionRecipe();Project rectangle=SectionFactory.Build(recipe,true);
 Yes(rectangle.Bars.Count==12,"rectangle corners counted once: 4+4+2+2=12");Near(rectangle.Bars.Sum(b=>b.Area),6080.4,1e-7,"D25 nominal areas retained");Near(SectionFactory.Offset(recipe),60,1e-10,"clear cover plus D10 tie plus half D25 gives 60 mm center offset");
 Near(rectangle.Bars.Max(b=>b.Y),340,1e-8,"height/cover converted to bar location");Yes(SectionFactory.Matches(Examples.Rectangle(),recipe),"legacy rectangle reproduced without manual coordinates");
 foreach(var p in new[]{Examples.Rectangle(),Examples.Circle(false),Examples.Circle(true),Examples.Manual2008(),Examples.AciReference(false),Examples.CircularReference(),Examples.BiaxialReference()})
 {string before=Files.Json(p);var inferred=SectionFactory.Infer(p);Yes(inferred!=null&&SectionFactory.Matches(p,inferred),"legacy geometry/areas inferred exactly "+p.Name);Yes(before==Files.Json(p),"inference does not mutate "+p.Name);}
 var arbitrary=AdditionalExamples.UnsymmetricalWall();Yes(SectionFactory.Infer(arbitrary)==null,"irregular wall stays custom rather than becoming rectangle");
 var asym=Examples.Rectangle();asym.Bars.RemoveAt(0);Yes(SectionFactory.Infer(asym)==null,"asymmetric reinforcement is not overwritten by a symmetric recipe");
 var ring=SectionFactory.Infer(Examples.Manual2008());Yes(ring.SecondRing&&ring.Count==50&&ring.InnerCount==45,"two concentric rings retain their separate counts");Near(ring.RingSpacing,100,1e-7,"two ring center spacing retained");
 var box=new SectionRecipe{Shape="Box",Width=1200,Height=900,InnerWidth=700,InnerHeight=450,ClearCover=50,Nx=5,Ny=4};var hollow=SectionFactory.Build(box,true);Near(Geometry.Region(hollow,0,1,0,true).Area,765000,1e-6,"box concrete opening deducted");Yes(hollow.Bars.Count==14,"box perimeter count");
 var circle=new SectionRecipe{Shape="HollowCircle",Width=1000,Height=1000,InnerWidth=560,Bar="D29",ClearCover=56.17,Count=16,SecondRing=true,InnerCount=12,RingSpacing=60};var circular=SectionFactory.Build(circle,true);Yes(circular.Bars.Count==28,"hollow circular two ring count");Near(Math.Sqrt(circular.Bars[0].X*circular.Bars[0].X+circular.Bars[0].Y*circular.Bars[0].Y),420,1e-8,"outer circular radius from clear cover");Near(Math.Sqrt(circular.Bars[16].X*circular.Bars[16].X+circular.Bars[16].Y*circular.Bars[16].Y),360,1e-8,"second circular radius from ring spacing");
 var capsule=new SectionRecipe{Shape="Stadium",Width=2400,Height=1000,ClearCover=50,Count=32};var stadium=SectionFactory.Build(capsule,true);Yes(stadium.Bars.Count==32,"stadium perimeter generates unique reinforcement");Near(Geometry.Region(stadium,0,1,0,true).Area,1400*1000+Math.PI*500*500,350,"stadium polygon area within 0.02 percent of ideal capsule");
 foreach(var invalid in new[]{new SectionRecipe{ClearCover=400},new SectionRecipe{Shape="HollowCircle",Width=600,InnerWidth=700},new SectionRecipe{Shape="Stadium",Width=500,Height=1000},new SectionRecipe{Nx=100,Ny=100}})
 {bool rejected=false;try{SectionFactory.Build(invalid,true);}catch(ArgumentException){rejected=true;}Yes(rejected,"invalid cover/opening/shape/count rejected "+invalid.Shape);}
 var restored=Files.Parse(Files.Json(circular));Yes(restored.SectionInput!=null&&SectionFactory.Matches(restored,restored.SectionInput),"saved project retains dimensions and reinforcement recipe");
 using(var form=new MainForm(null))
 {
     var tabs=(TabControl)Field(form,"inputTabs");Yes(tabs.TabPages[1].Text=="형상"&&tabs.TabPages[2].Text=="철근배근","input terminology changed");Yes(!((DataGridView)Field(form,"geometry")).Visible&&!((DataGridView)Field(form,"bars")).Visible,"coordinate grids hidden from primary workflow");
     var editor=(GuidedSectionEditor)Field(form,"sectionEditor");var p=(Project)Call(form,"Read");Yes(p.Bars.Count==12&&p.Loads.Count==3&&!editor.Pending,"unchanged primary inputs remain intact");
     ((DataGridView)Field(form,"material")).Rows[1].Cells[1].Value="35";Set(form,"result",new Analysis());editor.Change(q=>{q.Width=900;q.Nx=5;},false);
     Yes((bool)Field(form,"dirty")&&Field(form,"result")==null,"dimension edits invalidate results and participate in unsaved-change tracking");
     p=(Project)Call(form,"Read");Near(p.Contours[0].Points.Max(v=>v.X),450,1e-7,"calculate/save commits draft dimensions");Yes(p.Bars.Count==14&&p.Material.Fc==35&&p.Loads.Count==3,"guided apply preserves materials and load combinations");Yes(p.SectionInput!=null&&p.SectionInput.Width==900,"applied recipe persisted");
     var raw=(DataGridView)Field(form,"bars");raw.Rows[0].Cells[2].Value="123.456";p=(Project)Call(form,"Read");Yes(p.SectionInput==null&&Math.Abs(p.Bars[0].Area-123.456)<1e-10,"custom coordinate edits retain data and discard stale recipe");
     Call(form,"Fill",Examples.Rectangle());editor=(GuidedSectionEditor)Field(form,"sectionEditor");editor.Change(q=>q.ClearCover=999,false);bool failed=false;try{Call(form,"Read");}catch(TargetInvocationException ex){failed=ex.InnerException is ArgumentException;}Yes(failed,"invalid preview cannot enter analysis");
 }
 // Actual WinForms input components, rendered offscreen at the fixed window size.
 var original=Examples.Rectangle();var result=Engine.Analyze(original);
 using(var form=new MainForm(null))
 {
     Call(form,"Fill",original);Set(form,"calculated",original);Set(form,"result",result);Call(form,"ShowResults");((Panel)Field(form,"workspace")).BringToFront();((TabControl)Field(form,"views")).SelectedIndex=7;
     typeof(Form).GetMethod("OnLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{EventArgs.Empty});Layout(form);var tabs=(TabControl)Field(form,"inputTabs");float scale=(float)Field(form,"uiScale");
     foreach(int index in new[]{1,2})
     {
         tabs.SelectedIndex=index;Layout(tabs);var page=index==1?((GuidedSectionEditor)Field(form,"sectionEditor")).ShapePage:((GuidedSectionEditor)Field(form,"sectionEditor")).RebarPage;Layout(page);
         using(var image=new Bitmap(540,420))using(var g=Graphics.FromImage(image)){image.SetResolution(192,192);page.Sketch.Render(g,image.Width,image.Height);image.Save(Path.Combine(folder,index==1?"guided-shape-sketch.png":"guided-rebar-sketch.png"));}
         Yes(page.ApplyButton.Width/scale>210,"apply button fits input pane "+index);
     }
 }
 Console.WriteLine("PASS GUIDED total: "+count);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
