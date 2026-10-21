using System;using System.Collections.Generic;using System.Drawing;using System.Globalization;using System.Linq;using System.Windows.Forms;
namespace PMStudio
{
    public sealed class StandardForm : Form
    {
        public Project Project;readonly GuidedSectionEditor editor=new GuidedSectionEditor();
        public StandardForm()
        {
            SuspendLayout();AutoScaleMode=AutoScaleMode.None;Text="새 형상·철근배근";ClientSize=new Size(430,780);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font(Theme.FontName,9.5f);
            var tabs=new TabControl{Dock=DockStyle.Fill};foreach(var pair in new[]{new{Title="형상",Page=editor.ShapePage},new{Title="철근배근",Page=editor.RebarPage}}){var tab=new TabPage(pair.Title);tab.Controls.Add(pair.Page);tabs.TabPages.Add(tab);}Controls.Add(tabs);
            editor.LoadProject(Examples.Rectangle());editor.ApplyRequested=Create;editor.AdvancedRequested=bar=>{try{using(var dialog=new CoordinateEditorForm(editor.Build(),bar))if(dialog.ShowDialog(this)==DialogResult.OK){Project=dialog.Project;DialogResult=DialogResult.OK;Close();}}catch(ArgumentException ex){MessageBox.Show(this,ex.Message,"형상·철근배근");}};
            using(Graphics g=CreateGraphics()){float scale=g.DpiX/96;if(Math.Abs(scale-1)>.001f)Scale(new SizeF(scale,scale));editor.SetScale(scale);}ResumeLayout(true);
        }
        void Create(){try{editor.CommitEdits();Project=editor.Build();DialogResult=DialogResult.OK;Close();}catch(ArgumentException ex){MessageBox.Show(this,ex.Message,"형상·철근배근");}}
    }
    public sealed class CoordinateEditorForm : Form
    {
        readonly DataGridView shape=new DataGridView(),bars=new DataGridView();readonly Project original;
        public Project Project;
        public CoordinateEditorForm(Project p,bool rebar)
        {
            original=p;SuspendLayout();AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(640,680);Text="고급 편집 · 형상 / 철근배근 좌표";Font=new Font(Theme.FontName,9.5f);StartPosition=FormStartPosition.CenterParent;
            var tabs=new TabControl{Dock=DockStyle.Fill};TabPage a=new TabPage("형상 좌표"),b=new TabPage("철근배근 좌표");a.Controls.Add(shape);b.Controls.Add(bars);tabs.TabPages.Add(a);tabs.TabPages.Add(b);tabs.SelectedIndex=rebar?1:0;
            foreach(var grid in new[]{shape,bars}){grid.Dock=DockStyle.Fill;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.BackgroundColor=Color.White;grid.AllowUserToOrderColumns=false;grid.RowTemplate.Height=28;}
            foreach(string name in new[]{"형상 ID (0: 외곽)","X (mm)","Y (mm)"})shape.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=name,SortMode=DataGridViewColumnSortMode.NotSortable});
            foreach(string name in new[]{"X (mm)","Y (mm)","As (mm²)"})bars.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=name,SortMode=DataGridViewColumnSortMode.NotSortable});
            int hole=1;foreach(var c in p.Contours){int id=c.Hole?hole++:0;foreach(var v in c.Points)shape.Rows.Add(id,Files.N(v.X),Files.N(v.Y));}foreach(var v in p.Bars)bars.Rows.Add(Files.N(v.X),Files.N(v.Y),Files.N(v.Area));
            var hint=new Label{Text="사용자 형상·비정형 배근용 고급 편집입니다.\r\n윤곽 순서와 철근 중심 좌표를 입력합니다. 단위: mm / mm²",Dock=DockStyle.Top,Height=52,Padding=new Padding(8),ForeColor=Theme.Muted};
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(6)};var apply=new Button{Text="변경 적용",Width=110,Height=30};var cancel=new Button{Text="취소",Width=90,Height=30,DialogResult=DialogResult.Cancel};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);Controls.Add(tabs);Controls.Add(hint);Controls.Add(buttons);CancelButton=cancel;apply.Click+=(s,e)=>Apply();
            using(Graphics g=CreateGraphics()){float scale=g.DpiX/96;if(Math.Abs(scale-1)>.001f)Scale(new SizeF(scale,scale));}ResumeLayout(true);
        }
        static double Number(DataGridView grid,int i,int col)
        {double v;object value=grid.Rows[i].Cells[col].Value;if(value==null||!double.TryParse(value.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||!Engine.Finite(v))throw new ArgumentException("행 "+(i+1)+"의 숫자를 확인하세요.");return v;}
        void Apply()
        {
            try
            {
                shape.EndEdit();bars.EndEdit();var p=Files.Clone(original);p.Contours.Clear();p.Bars.Clear();var done=new HashSet<int>();int previous=-1;var contours=new Dictionary<int,Contour>();
                foreach(DataGridViewRow row in shape.Rows)if(!row.IsNewRow)
                {double number=Number(shape,row.Index,0);if(number!=Math.Truncate(number)||number<0||number>8)throw new ArgumentException("형상 ID는 0~8의 정수입니다.");int id=(int)number;if(id!=previous){if(done.Contains(id))throw new ArgumentException("같은 형상 ID의 점은 연속 입력하세요.");if(previous>=0)done.Add(previous);previous=id;}if(!contours.ContainsKey(id)){var c=new Contour{Hole=id!=0};contours.Add(id,c);p.Contours.Add(c);}contours[id].Points.Add(new Point2(Number(shape,row.Index,1),Number(shape,row.Index,2)));}
                foreach(DataGridViewRow row in bars.Rows)if(!row.IsNewRow)p.Bars.Add(new Rebar(Number(bars,row.Index,0),Number(bars,row.Index,1),Number(bars,row.Index,2)));
                bool geometryChanged=Files.Json(p.Contours)!=Files.Json(original.Contours);if(geometryChanged)p.CircularRadius=0;p.SectionInput=null;Engine.Validate(p);Project=p;DialogResult=DialogResult.OK;Close();
            }catch(ArgumentException ex){MessageBox.Show(this,ex.Message,"고급 편집");}
        }
    }
}
