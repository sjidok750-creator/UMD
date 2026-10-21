using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PMStudio;

static class LayoutTests
{
    static int count;
    static void Yes(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS LAYOUT "+message);}
    static object Field(MainForm f,string name){return typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static void Set(MainForm f,string name,object value){typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,value);}
    static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
    static void Layout(Control c){c.CreateControl();c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Project p=Examples.Manual2008();Analysis r=Engine.Analyze(p);string folder=args[0];
            using(MainForm form=new MainForm(null))
            {
                Call(form,"Fill",p);Set(form,"calculated",p);Set(form,"result",r);Call(form,"ShowResults");
                ((Panel)Field(form,"workspace")).BringToFront();TabControl views=(TabControl)Field(form,"views");views.SelectedIndex=7;
                typeof(Form).GetMethod("OnLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{EventArgs.Empty});Layout(form);
                float scale=(float)Field(form,"uiScale");Yes(form.FormBorderStyle==FormBorderStyle.FixedSingle&&!form.MaximizeBox,"window dimensions fixed without maximize");
                Yes(Math.Abs(form.ClientSize.Width/scale-1120)<2&&Math.Abs(form.ClientSize.Height/scale-910)<2,"1120 by 910 DIP client target");
                var split=(SplitContainer)Field(form,"mainSplit");Yes(Math.Abs(split.Panel1.Width/scale-300)<3,"input pane 300 DIP");
                Yes(((SplitContainer)Field(form,"resultsSplit")).Panel2Collapsed,"duplicate bottom table hidden in report view");
                ToolStrip toolbar=(ToolStrip)Field(form,"tools");ToolStripItem[] items=toolbar.Items.Cast<ToolStripItem>().Where(item=>item.Placement==ToolStripItemPlacement.Main).ToArray();
                Yes(items.All(i=>i.Bounds.Right<=toolbar.ClientSize.Width&&i.Bounds.Top>=0&&i.Bounds.Bottom<=toolbar.ClientSize.Height),"toolbar items stay inside toolbar");
                for(int i=1;i<items.Length;i++)Yes(!items[i-1].Bounds.IntersectsWith(items[i].Bounds),"toolbar items do not overlap "+i);
                foreach(TabControl tabs in new[]{views,(TabControl)Field(form,"inputTabs")})
                {
                    bool overlap=false;for(int i=0;i<tabs.TabCount;i++)for(int j=i+1;j<tabs.TabCount;j++)if(tabs.GetTabRect(i).IntersectsWith(tabs.GetTabRect(j)))overlap=true;
                    Yes(!overlap,"tab headers do not overlap");
                }
                ReportSheet report=(ReportSheet)Field(form,"reportSheet");Layout(report);DataGridView grid=report.SummaryGrid;
                Yes(!grid.Bounds.IntersectsWith(report.CaseCanvas.Bounds),"table and graph do not overlap");Yes(report.CaseCanvas.Width/scale>750&&report.CaseCanvas.Height/scale>420,"detail graph retains readable viewport");
                using(Graphics g=grid.CreateGraphics()){Yes(grid.ColumnHeadersHeight>=grid.Font.GetHeight(g)*2+4,"two-line headers have measured height");Yes(grid.Rows.Cast<DataGridViewRow>().All(row=>row.Height>=grid.Font.GetHeight(g)+4),"row heights fit Gothic text");}
                Yes(grid.Columns.Cast<DataGridViewColumn>().All(c=>c.Width>=70),"summary columns retain minimum readable widths");
                report.CaseSelector.SelectedIndex=1;Yes(report.CaseCanvas.CaseIndex==1,"case selector changes detailed curve");
                foreach(int dpi in new[]{96,120,144,192})using(Bitmap image=new Bitmap(810*dpi/96,520*dpi/96))
                {image.SetResolution(dpi,dpi);using(Graphics g=Graphics.FromImage(image))report.CaseCanvas.Render(g,image.Width,image.Height);image.Save(Path.Combine(folder,"detail-dpi-"+dpi+".png"));Yes(true,"detailed diagram renders at "+dpi+" DPI");}
                // Native screenshots are verified separately through computer-use.
                Yes(report.SummaryGrid.Rows.Cast<DataGridViewRow>().All(row=>row.Cells[5].Value.ToString()=="편람 대조 미확정"),"manual discrepancy visible in native table");
            }
            Console.WriteLine("PASS LAYOUT total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
