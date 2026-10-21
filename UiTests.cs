using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using PMStudio;

static class UiTests
{
    static int count;
    static object Field(MainForm f,string name) { return typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f); }
    static void Set(MainForm f,string name,object value) { typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,value); }
    static object Call(MainForm f,string name,params object[] args) { return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args); }
    static void Yes(bool b,string text) { if(!b)throw new Exception(text);count++;Console.WriteLine("PASS UI "+text); }
    [STAThread]static int Main(string[] args)
    {
        try
        {
            using(MainForm f=new MainForm(null))
            {
                Project p=(Project)Call(f,"Read");Yes(p.Bars.Count==12&&p.Loads.Count==3,"form input contract");
                Analysis r=Engine.Analyze(p);Set(f,"calculated",p);Set(f,"result",r);Call(f,"ShowResults");
                Yes(((DataGridView)Field(f,"checks")).Rows.Count==3,"result grid populated");
                string path=Path.Combine(args[0],"ui-contract.pmproj.json");Call(f,"WriteProject",path);Yes(Files.Parse(File.ReadAllText(path)).Bars.Count==12,"form project save roundtrip");Yes(Field(f,"result")!=null,"unchanged save preserves results");
                ((DataGridView)Field(f,"material")).Rows[1].Cells[1].Value="35";
                Yes(Field(f,"result")==null&&((DataGridView)Field(f,"checks")).Rows.Count==0,"input change invalidates all results");Yes(((Plot)Field(f,"section")).Project==null,"input change clears stale section view");
                Call(f,"OpenPath",path);Yes((string)((DataGridView)Field(f,"material")).Rows[1].Cells[1].Value=="30"&&Field(f,"result")==null,"open restores data and requires calculation");
                bool sortable=false;foreach(string name in new[]{"material","geometry","bars","loads"})foreach(DataGridViewColumn c in ((DataGridView)Field(f,name)).Columns)if(c.SortMode!=DataGridViewColumnSortMode.NotSortable)sortable=true;Yes(!sortable,"coordinate and material row order protected");
            }
            using(MainForm f=new MainForm(null))
            {
                DataGridView bars=(DataGridView)Field(f,"bars");bars.Rows[0].Cells[2].Value="490.873852123";bars.Rows[1].Cells[2].Value="123.456";bars.ClearSelection();bars.Rows[0].Cells[2].Selected=true;Call(f,"ApplyBarSize","D25");Project changed=(Project)Call(f,"Read");Yes(Math.Abs(changed.Bars[0].Area-506.7)<1e-10,"selected bar uses catalog D25 area");Yes(Math.Abs(changed.Bars[1].Area-123.456)<1e-10,"unselected custom area preserved");
            }
            Console.WriteLine("PASS UI total: "+count);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
