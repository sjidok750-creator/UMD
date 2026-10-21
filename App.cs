using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PMStudio
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args)
        {
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length>0?args[0]:null));
        }
    }
    public sealed class MainForm : Form
    {
        Project project=Examples.Rectangle();
        Analysis result;
        Project calculated;
        bool filling,busy,dirty;
        int nextView=7;
        string filePath;
        ApiServer api;
        readonly Color lavender=Theme.Background;
        readonly Dashboard overview=new Dashboard();
        readonly ReportSheet reportSheet=new ReportSheet();
        readonly GuidedSectionEditor sectionEditor=new GuidedSectionEditor();
        SplitContainer mainSplit,resultsSplit;
        float uiScale=1;
        readonly TabControl inputTabs=new TabControl{Dock=DockStyle.Fill},views=new TabControl{Dock=DockStyle.Fill};
        readonly DataGridView material=Grid(),geometry=Grid(),bars=Grid(),loads=Grid(),checks=Grid(),steel=Grid();
        readonly Plot section=new Plot(),pmx=new Plot{Mode="PMx"},pmy=new Plot{Mode="PMy"},slice=new Plot{Mode="Slice"},surface=new Plot{Mode="Surface"};
        readonly TextBox detail=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.White};
        readonly TextBox log=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.FromArgb(248,248,248)};
        readonly ToolStripStatusLabel status=new ToolStripStatusLabel("준비 / Ready"),apiStatus=new ToolStripStatusLabel("API 중지");
        readonly Panel welcome=new Panel{Dock=DockStyle.Fill},workspace=new Panel{Dock=DockStyle.Fill};
        readonly ToolStrip tools=new ToolStrip{Dock=DockStyle.Top,GripStyle=ToolStripGripStyle.Hidden,AutoSize=false,Height=36,Padding=new Padding(8,2,8,2),CanOverflow=true};
        readonly Label summary=new Label{Dock=DockStyle.Top,Height=29,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0),BackColor=Color.FromArgb(235,233,247),ForeColor=Color.Navy};
        readonly MenuStrip menu=new MenuStrip();
        public MainForm(string path)
        {
            SuspendLayout();AutoScaleMode=AutoScaleMode.None;
            Text="PM Studio +  |  단면내력 전문 해석";ClientSize=new Size(1120,910);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
            Font=new Font(Theme.FontName,9.5f);BackColor=lavender;WindowState=FormWindowState.Normal;
            BuildMenu();BuildWorkspace();BuildWelcome();
            StatusStrip strip=new StatusStrip{BackColor=lavender};status.Spring=true;status.TextAlign=ContentAlignment.MiddleLeft;strip.Items.Add(status);strip.Items.Add(apiStatus);
            Panel body=new Panel{Dock=DockStyle.Fill};body.Controls.Add(workspace);body.Controls.Add(welcome);welcome.BringToFront();
            Controls.Add(body);Controls.Add(new Brand{Dock=DockStyle.Top});Controls.Add(menu);Controls.Add(strip);MainMenuStrip=menu;
            menu.Font=new Font(Theme.FontName,9.5f);tools.Font=menu.Font;strip.Font=new Font(Theme.FontName,9);status.AutoToolTip=true;
            Fill(project);UpdateCaption();
            FormClosing+=ClosingApp;
            if(path!=null)Shown+=(s,e)=>OpenPath(path);
            // Runtime-created controls need one explicit scale after the entire tree exists.
            // Keeping automatic scaling off avoids scaling the font/layout tree twice.
            using(Graphics display=CreateGraphics()){uiScale=display.DpiX/96f;if(Math.Abs(uiScale-1)>0.001f)Scale(new SizeF(uiScale,uiScale));}
            MinimumSize=Size;MaximumSize=Size;
            ResumeLayout(true);
            ApplyGridMetrics();sectionEditor.SetScale(uiScale);
        }
        static DataGridView Grid()
        {
            DataGridView grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.FixedSingle,GridColor=Color.FromArgb(225,229,233),AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersWidth=28,AllowUserToOrderColumns=false,SelectionMode=DataGridViewSelectionMode.CellSelect,MultiSelect=false,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
            grid.RowTemplate.Height=29;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(237,242,239);grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Ink;grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(221,237,227);grid.DefaultCellStyle.SelectionForeColor=Theme.Ink;grid.ShowCellToolTips=true;return grid;
        }
        static void Columns(DataGridView g,params string[] names)
        {
            foreach(string n in names)g.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=n,Name="C"+g.Columns.Count,SortMode=DataGridViewColumnSortMode.NotSortable});
            g.DataError+=(s,e)=> { e.ThrowException=false;MessageBox.Show("입력값을 확인하세요.","입력 오류"); };
        }
        Button Button(string title,Action action,int width)
        {
            Button b=new Button{Text=title,Width=width,Height=29,BackColor=Color.FromArgb(225,225,225),UseVisualStyleBackColor=false,FlatStyle=FlatStyle.Standard};b.Click+=(s,e)=>Guard(action);return b;
        }
        void Item(ToolStripMenuItem parent,string title,Action action,Keys keys)
        {
            ToolStripMenuItem item=new ToolStripMenuItem(title);item.ShortcutKeys=keys;item.Click+=(s,e)=>Guard(action);parent.DropDownItems.Add(item);
        }
        void BuildMenu()
        {
            menu.BackColor=lavender;ToolStripMenuItem f=new ToolStripMenuItem("파일(&F)");menu.Items.Add(f);
            Item(f,"새 프로젝트",()=>NewProject(Examples.Rectangle()),Keys.Control|Keys.N);Item(f,"열기...",Open,Keys.Control|Keys.O);Item(f,"저장",Save,Keys.Control|Keys.S);Item(f,"다른 이름으로 저장...",SaveAs,Keys.None);f.DropDownItems.Add(new ToolStripSeparator());Item(f,"종료",Close,Keys.Alt|Keys.F4);
            ToolStripMenuItem m=new ToolStripMenuItem("단면(&S)");menu.Items.Add(m);Item(m,"표준단면 생성...",Standard,Keys.None);Item(m,"사각형 예제",()=>NewProject(Examples.Rectangle()),Keys.None);Item(m,"원형 예제",()=>NewProject(Examples.Circle(false)),Keys.None);Item(m,"중공 원형 예제",()=>NewProject(Examples.Circle(true)),Keys.None);Item(m,"가상 교각 보고서 예제",()=>NewProject(Examples.Pier()),Keys.None);Item(m,"도로설계편람 2008 원형 교각",()=>NewProject(Examples.Manual2008()),Keys.None);Item(m,"공개 검증 · 원형 8철근",()=>NewProject(Examples.CircularReference()),Keys.None);Item(m,"공개 검증 · 직사각형 이축휨",()=>NewProject(Examples.BiaxialReference()),Keys.None);Item(m,"추가 검증 · ACI E702.2 (공칭)",()=>NewProject(AdditionalExamples.AciE702()),Keys.None);Item(m,"추가 검증 · 비대칭 벽체 (공칭)",()=>NewProject(AdditionalExamples.UnsymmetricalWall()),Keys.None);Item(m,"추가 검증 · 편심 개구부 상자",()=>NewProject(AdditionalExamples.HollowBox()),Keys.None);Item(m,"ACI 공개 검증예제 (Grade 60)",()=>NewProject(Examples.AciReference(false)),Keys.None);Item(m,"ACI 공개 검증예제 (Grade 80)",()=>NewProject(Examples.AciReference(true)),Keys.None);
            ToolStripMenuItem c=new ToolStripMenuItem("해석(&A)");menu.Items.Add(c);Item(c,"상관면 계산",Calculate,Keys.F5);Item(c,"보고서 그림 PNG...",ExportImage,Keys.None);Item(c,"통합 탐색 그림 PNG...",ExportOverview,Keys.None);Item(c,"결과 CSV...",ExportCsv,Keys.None);Item(c,"결과 HTML...",ExportHtml,Keys.None);
            ToolStripMenuItem a=new ToolStripMenuItem("연결(&I)");menu.Items.Add(a);Item(a,"로컬 API 시작 / 중지",ToggleApi,Keys.None);
            Item(c,"검토 요약표 PNG...",ExportTableImage,Keys.None);Item(c,"검토 요약표 CSV...",ExportSummaryCsv,Keys.None);
            ToolStripMenuItem h=new ToolStripMenuItem("도움말(&H)");menu.Items.Add(h);Item(h,"시작 화면",()=>{welcome.BringToFront();},Keys.None);Item(h,"모델 / 단위 / 적용 범위",About,Keys.None);
        }
        void BuildWelcome()
        {
            welcome.BackColor=lavender;
            TableLayoutPanel t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(35,28,35,28)};
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));
            t.RowStyles.Add(new RowStyle(SizeType.Percent,14));t.RowStyles.Add(new RowStyle(SizeType.Percent,72));t.RowStyles.Add(new RowStyle(SizeType.Percent,14));
            Panel card=new Panel{Dock=DockStyle.Fill,BorderStyle=BorderStyle.Fixed3D,BackColor=lavender,Padding=new Padding(25)};
            TableLayoutPanel inside=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=6};inside.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));inside.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            foreach(float h in new float[]{26,12,12,22,14,14})inside.RowStyles.Add(new RowStyle(SizeType.Percent,h));
            Label title=new Label{Text="Welcome to PM Studio +",Dock=DockStyle.Fill,ForeColor=Color.Blue,Font=new Font(Theme.FontName,26,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,BorderStyle=BorderStyle.Fixed3D};inside.Controls.Add(title,0,0);inside.SetColumnSpan(title,2);
            Label sub=new Label{Text="RC / P–M / Biaxial / Explore the section…",ForeColor=Color.DarkMagenta,Font=new Font(Font.FontFamily,12,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter};inside.Controls.Add(sub,0,1);inside.SetColumnSpan(sub,2);
            Label info=new Label{Text="단면·배근을 정의하고, 축력과 양축 휨의\r\n공칭내력을 확인하세요.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};inside.Controls.Add(info,0,2);
            Label model=new Label{Text="현재 모델: RC 등가 직사각형 응력블록\r\n단위: mm · MPa · kN · kN·m\r\nKDS 설계판정 모듈은 후속 개발 예정",Dock=DockStyle.Fill,ForeColor=Color.Navy,TextAlign=ContentAlignment.MiddleLeft};inside.Controls.Add(model,0,3);
            Button start=Button("예제 열고 시작하기  →",()=> { workspace.BringToFront();Calculate(); },300);start.Dock=DockStyle.Fill;inside.Controls.Add(start,0,4);
            Button custom=Button("표준단면 새로 만들기...",Standard,300);custom.Dock=DockStyle.Fill;inside.Controls.Add(custom,0,5);
            Plot diagram=new Plot{Dock=DockStyle.Fill,Project=Examples.Rectangle(),Compact=true};inside.Controls.Add(diagram,1,2);inside.SetRowSpan(diagram,4);
            card.Controls.Add(inside);t.Controls.Add(card,1,1);welcome.Controls.Add(t);
            Label version=new Label{Text="PM Studio +  Development Preview 0.8   |   독립 계산엔진 · Windows · Local API",Dock=DockStyle.Bottom,Height=36,Padding=new Padding(14,0,0,0),ForeColor=Color.Navy};welcome.Controls.Add(version);
        }
        void BuildWorkspace()
        {
            workspace.BackColor=lavender;
            ToolButton("계산 (F5)",Calculate);ToolButton("입력 접기 / 펼치기",()=>{mainSplit.Panel1Collapsed=!mainSplit.Panel1Collapsed;});ToolButton("형상·배근",()=>{workspace.BringToFront();inputTabs.SelectedIndex=1;});ToolButton("저장",Save);
            ToolStripDropDownButton export=new ToolStripDropDownButton("내보내기");tools.Items.Add(export);
            foreach(var action in new[]{new KeyValuePair<string,Action>("보고서용 상관도 PNG",ExportDetailImage),new KeyValuePair<string,Action>("보고서용 상관도 EMF (벡터)",ExportDetailMetafile),new KeyValuePair<string,Action>("전체 요약시트 PNG",ExportImage),new KeyValuePair<string,Action>("요약표 PNG",ExportTableImage),new KeyValuePair<string,Action>("요약표 CSV",ExportSummaryCsv),new KeyValuePair<string,Action>("전체 곡선 CSV",ExportCsv),new KeyValuePair<string,Action>("검토 HTML",ExportHtml),new KeyValuePair<string,Action>("통합 탐색 PNG",ExportOverview)}){Action run=action.Value;export.DropDownItems.Add(action.Key,null,(s,e)=>Guard(run));}
            ToolButton("API 시작 / 중지",ToggleApi);
            SplitContainer horizontal=new SplitContainer{Size=new Size(1300,700),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=5};
            resultsSplit=horizontal;
            SplitContainer main=new SplitContainer{Size=new Size(1300,550),Dock=DockStyle.Fill,SplitterWidth=5,IsSplitterFixed=true};main.Panel1MinSize=300;main.Panel2MinSize=500;
            mainSplit=main;
            Columns(material,"항목","값");material.AllowUserToAddRows=false;material.AllowUserToDeleteRows=false;material.Columns[0].ReadOnly=true;material.Columns[0].FillWeight=135;
            Columns(geometry,"형상 ID","X (mm)","Y (mm)");Columns(bars,"X (mm)","Y (mm)","As (mm²)");Columns(loads,"조합명","P (kN)","Mx (kN·m)","My (kN·m)");
            AddInput("재료",material,"RC 단면 입력 · mm / MPa / kN / kN·m\r\n계수 모드와 공제법은 목록에서 선택합니다.");
            AddGuidedInput("형상",sectionEditor.ShapePage,geometry);
            AddGuidedInput("철근배근",sectionEditor.RebarPage,bars);
            sectionEditor.ApplyRequested=()=>Guard(ApplyGuidedSection);sectionEditor.DraftChanged=InvalidateResult;sectionEditor.AdvancedRequested=bar=>Guard(()=>AdvancedSection(bar));
            ContextMenuStrip rebarMenu=new ContextMenuStrip();foreach(RebarSize size in RebarCatalog.Sizes){string name=size.Name;rebarMenu.Items.Add("선택 행에 "+size.ToString(),null,(s,e)=>Guard(()=>ApplyBarSize(name)));}bars.ContextMenuStrip=rebarMenu;
            bars.CellMouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right&&e.RowIndex>=0&&!bars.Rows[e.RowIndex].IsNewRow&&!bars.Rows[e.RowIndex].Cells[Math.Max(0,e.ColumnIndex)].Selected){bars.ClearSelection();bars.CurrentCell=bars.Rows[e.RowIndex].Cells[Math.Max(0,e.ColumnIndex)];}};
            AddInput("하중",loads,"압축 P (+) · 좌표계와 부호 확인\r\n동일 조합의 P·Mx·My를 입력하세요.");
            main.Panel1.Controls.Add(inputTabs);
            AddView("통합",overview);AddView("단면",section);AddView("P–Mx",pmx);AddView("P–My",pmy);AddView("Mx–My",slice);AddView("3D",surface);
            SplitContainer point=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal};point.Panel1.Controls.Add(detail);point.Panel2.Controls.Add(steel);AddView("계산점 상세",point);
            Columns(steel,"철근","εs","fs (MPa)","Fs (kN)");steel.ReadOnly=true;steel.AllowUserToAddRows=false;
            AddView("보고서 / 요약표",reportSheet);reportSheet.Pick=SelectPoint;
            main.Panel2.Controls.Add(views);main.Panel2.Controls.Add(summary);
            horizontal.Panel1.Controls.Add(main);
            TabControl bottom=new TabControl{Dock=DockStyle.Fill};TabPage cr=new TabPage("조합별 검토 · 더블클릭: 축력 절단"),lg=new TabPage("계산 기록");cr.Controls.Add(checks);lg.Controls.Add(log);bottom.TabPages.Add(cr);bottom.TabPages.Add(lg);horizontal.Panel2.Controls.Add(bottom);
            Columns(checks,"하중조합","P (kN)","Mx (kN·m)","My (kN·m)","공칭영역","모멘트 사용률","설명");checks.Columns[6].FillWeight=280;checks.ReadOnly=true;checks.AllowUserToAddRows=false;
            workspace.Controls.Add(horizontal);workspace.Controls.Add(tools);
            views.Multiline=true;inputTabs.Multiline=true;summary.AutoEllipsis=true;
            views.SelectedIndexChanged+=(s,e)=>{horizontal.Panel2Collapsed=views.SelectedIndex==7;};
            Load+=(s,e)=> { main.SplitterDistance=(int)(300*uiScale);horizontal.SplitterDistance=Math.Max(200,horizontal.Height-(int)(165*uiScale));point.SplitterDistance=Math.Max(100,point.Height/2);horizontal.Panel2Collapsed=views.SelectedIndex==7; };
            foreach(DataGridView g in new[]{material,geometry,bars,loads}) { g.CellValueChanged+=(s,e)=>InvalidateResult();g.RowsRemoved+=(s,e)=>InvalidateResult();g.UserAddedRow+=(s,e)=>InvalidateResult(); }
            foreach(Plot p in new[]{pmx,pmy,slice})p.Pick=SelectPoint;
            overview.Curve.Pick=SelectPoint;
            checks.CellDoubleClick+=(s,e)=> { if(e.RowIndex<0||calculated==null||busy)return;double axial=calculated.Loads[e.RowIndex].P;material.Rows[9].Cells[1].Value=F(axial);nextView=4;Guard(Calculate); };
        }
        void ApplyGridMetrics()
        {
            foreach(DataGridView grid in new[]{material,geometry,bars,loads,checks,steel})
            {grid.RowTemplate.Height=(int)(28*uiScale);foreach(DataGridViewRow row in grid.Rows)row.Height=grid.RowTemplate.Height;grid.AutoResizeColumnHeadersHeight();}
        }
        void ToolButton(string title,Action action){ToolStripButton button=new ToolStripButton(title){AutoSize=true,DisplayStyle=ToolStripItemDisplayStyle.Text,Margin=new Padding(3,1,8,1)};button.Click+=(s,e)=>Guard(action);tools.Items.Add(button);}
        void AddInput(string title,Control grid,string hint)
        {
            TabPage tab=new TabPage(title){BackColor=lavender};Label label=new Label{Text=hint,Dock=DockStyle.Top,Height=58,Padding=new Padding(5),ForeColor=Color.Navy};tab.Controls.Add(grid);tab.Controls.Add(label);inputTabs.TabPages.Add(tab);
        }
        void AddGuidedInput(string title,Control page,DataGridView raw)
        {
            var tab=new TabPage(title){BackColor=Theme.Background};raw.Visible=false;tab.Controls.Add(raw);tab.Controls.Add(page);page.BringToFront();inputTabs.TabPages.Add(tab);
        }
        void ApplyGuidedSection()
        {
            Project p=sectionEditor.Build();filling=true;try
            {
                geometry.Rows.Clear();int hole=1;foreach(Contour c in p.Contours){int id=c.Hole?hole++:0;foreach(Point2 v in c.Points)geometry.Rows.Add(id,F(v.X),F(v.Y));}
                bars.Rows.Clear();foreach(Rebar b in p.Bars)bars.Rows.Add(F(b.X),F(b.Y),F(b.Area));material.Rows[12].Cells[1].Value=F(p.CircularRadius);sectionEditor.Accept(p);
            }finally{filling=false;}
            InvalidateResult();section.Project=p;section.Invalidate();overview.Bind(p,null);status.Text="형상·철근배근 적용 완료 · "+p.Bars.Count+"개 · As "+p.Bars.Sum(b=>b.Area).ToString("N2")+" mm²";
        }
        void AdvancedSection(bool rebar)
        {
            Project current=Read();using(var dialog=new CoordinateEditorForm(current,rebar))if(dialog.ShowDialog(this)==DialogResult.OK)
            {int selected=inputTabs.SelectedIndex;Fill(dialog.Project);dirty=true;inputTabs.SelectedIndex=selected;UpdateCaption();status.Text="고급 편집 적용 완료 · 다시 계산하세요.";}
        }
        void AddView(string title,Control control) { TabPage tab=new TabPage(title){BackColor=Color.White};control.Dock=DockStyle.Fill;tab.Controls.Add(control);views.TabPages.Add(tab); }
        void Guard(Action a) { try { if(busy)throw new InvalidOperationException("계산이 끝난 뒤 실행해 주세요.");a(); }catch(Exception ex) { status.Text=ex.Message;MessageBox.Show(this,ex.Message,"PM Studio +",MessageBoxButtons.OK,MessageBoxIcon.Information); } }
        static string F(double d) { return d.ToString("G12",CultureInfo.InvariantCulture); }
        void ApplyBarSize(string name)
        {double area=RebarCatalog.Get(name).Area;int[] rows=bars.SelectedCells.Cast<DataGridViewCell>().Select(c=>c.RowIndex).Distinct().Where(i=>!bars.Rows[i].IsNewRow).ToArray();foreach(int i in rows)bars.Rows[i].Cells[2].Value=F(area);status.Text=rows.Length+"개 철근에 "+name+" / "+F(area)+" mm² 적용";}
        void Fill(Project p)
        {
            filling=true;try
            {
                project=p;material.Rows.Clear();string[] titles={"프로젝트 이름","Fc (MPa)","Fy (MPa)","Es (MPa)","εcu","α (응력블록)","β (a/c)","방향 수 (24~144)","깊이 분할 수","절단 P (kN)","계수 모드","철근 공제법","원형 적분 R (mm)"};
                object[] values={p.Name,F(p.Material.Fc),F(p.Material.Fy),F(p.Material.Es),F(p.Material.EpsCu),F(p.Material.Alpha),F(p.Material.Beta),p.AngleCount,p.DepthCount,F(p.SliceP),p.ReductionProfile,p.BarDisplacement,F(p.CircularRadius)};
                for(int i=0;i<titles.Length;i++)material.Rows.Add(titles[i],values[i]);
                material.Rows[10].Cells[1]=new DataGridViewComboBoxCell{DataSource=new[]{new KeyValuePair<string,string>("Nominal","공칭내력"),new KeyValuePair<string,string>("Reference-ACI31819-Tied","ACI 검산"),new KeyValuePair<string,string>("Reference-KR2008-Tied","편람 2008 검산"),new KeyValuePair<string,string>("Reference-ACI31819-Spiral","ACI 원형 검산")},ValueMember="Key",DisplayMember="Value",Value=p.ReductionProfile};
                material.Rows[11].Cells[1]=new DataGridViewComboBoxCell{DataSource=new[]{new KeyValuePair<string,string>("ExactDisk","원형면적 적분"),new KeyValuePair<string,string>("CenterLumped","중심기준 공제")},ValueMember="Key",DisplayMember="Value",Value=p.BarDisplacement};
                material.Columns[0].FillWeight=115;material.Columns[1].FillWeight=100;
                geometry.Rows.Clear();int hole=1;foreach(Contour c in p.Contours){int id=c.Hole?hole++:0;foreach(Point2 q in c.Points)geometry.Rows.Add(id,F(q.X),F(q.Y));}
                bars.Rows.Clear();foreach(Rebar b in p.Bars)bars.Rows.Add(F(b.X),F(b.Y),F(b.Area));loads.Rows.Clear();foreach(Demand d in p.Loads)loads.Rows.Add(d.Name,F(d.P),F(d.Mx),F(d.My));
                ResetResults();section.Project=p;section.Invalidate();overview.Bind(p,null);dirty=false;ApplyGridMetrics();sectionEditor.LoadProject(p);
            }finally{filling=false;}UpdateCaption();
        }
        string Cell(DataGridView g,int row,int col)
        {
            object v=g.Rows[row].Cells[col].Value;string s=v==null?"":v.ToString().Trim();if(s.Length==0)throw new ArgumentException(string.Format("빈 입력: 행 {0}, 열 {1}",row+1,col+1));return s;
        }
        double Number(DataGridView g,int row,int col) { double d;if(!double.TryParse(Cell(g,row,col),NumberStyles.Float,CultureInfo.InvariantCulture,out d)||!Engine.Finite(d))throw new ArgumentException(string.Format("숫자 입력 오류: 행 {0}, 열 {1}. 소수점은 '.'입니다.",row+1,col+1));return d; }
        int Integer(DataGridView g,int row,int col) { double d=Number(g,row,col);if(d!=Math.Truncate(d)||d<int.MinValue||d>int.MaxValue)throw new ArgumentException("정수를 입력하세요.");return (int)d; }
        Project Read()
        {
            sectionEditor.CommitEdits();if(sectionEditor.Pending)ApplyGuidedSection();
            foreach(DataGridView g in new[]{material,geometry,bars,loads}) { if(!g.EndEdit())throw new ArgumentException("현재 셀의 입력값을 확인하세요."); }
            Project p=new Project{Name=Cell(material,0,1)};p.Material=new Material{Fc=Number(material,1,1),Fy=Number(material,2,1),Es=Number(material,3,1),EpsCu=Number(material,4,1),Alpha=Number(material,5,1),Beta=Number(material,6,1)};
            p.AngleCount=Integer(material,7,1);p.DepthCount=Integer(material,8,1);p.SliceP=Number(material,9,1);p.ReductionProfile=Cell(material,10,1);p.BarDisplacement=Cell(material,11,1);p.CircularRadius=Number(material,12,1);
            Dictionary<int,Contour> cs=new Dictionary<int,Contour>();HashSet<int> finished=new HashSet<int>();int prev=-1;
            foreach(DataGridViewRow row in geometry.Rows)if(!row.IsNewRow)
            {
                int i=row.Index,id=Integer(geometry,i,0);if(id<0||id>8)throw new ArgumentException("형상 ID는 0~8입니다.");
                if(id!=prev) { if(finished.Contains(id))throw new ArgumentException("같은 형상 ID의 꼭짓점은 연속 입력하세요.");if(prev>=0)finished.Add(prev);prev=id; }
                if(!cs.ContainsKey(id)){cs[id]=new Contour{Hole=id!=0};p.Contours.Add(cs[id]);}cs[id].Points.Add(new Point2(Number(geometry,i,1),Number(geometry,i,2)));
            }
            foreach(DataGridViewRow row in bars.Rows)if(!row.IsNewRow)p.Bars.Add(new Rebar(Number(bars,row.Index,0),Number(bars,row.Index,1),Number(bars,row.Index,2)));
            foreach(DataGridViewRow row in loads.Rows)if(!row.IsNewRow)p.Loads.Add(new Demand{Name=Cell(loads,row.Index,0),P=Number(loads,row.Index,1),Mx=Number(loads,row.Index,2),My=Number(loads,row.Index,3)});
            if(sectionEditor.AppliedRecipe!=null&&SectionFactory.Matches(p,sectionEditor.AppliedRecipe))p.SectionInput=sectionEditor.AppliedRecipe.Copy();Engine.Validate(p);return p;
        }
        void ResetResults()
        {
            result=null;calculated=null;checks.Rows.Clear();steel.Rows.Clear();detail.Clear();summary.Text="RC 공칭내력 / 기준 모듈 미적용 / 결과 없음";
            foreach(Plot p in new[]{section,pmx,pmy,slice,surface}) { p.Result=null;p.Selected=null;if(p!=section)p.Project=null;p.Invalidate(); }
            overview.Bind(null,null);reportSheet.Bind(null,null);
        }
        void InvalidateResult() { if(filling)return;dirty=true;ResetResults();section.Project=null;section.Invalidate();status.Text="입력이 변경되었습니다. 재계산이 필요합니다.";UpdateCaption(); }
        void UpdateCaption() { Text="PM Studio +  |  "+project.Name+(dirty?" *":"")+"  |  Development 0.8"; }
        public void Calculate()
        {
            Project input=Read();project=input;workspace.BringToFront();ResetResults();section.Project=input;section.Invalidate();
            busy=true;inputTabs.Enabled=false;tools.Enabled=false;menu.Enabled=false;UseWaitCursor=true;status.Text="상관면 / 축력 절단 / 방향 수렴 비교 계산 중...";
            BackgroundWorker worker=new BackgroundWorker();DateTime start=DateTime.UtcNow;
            worker.DoWork+=(s,e)=> { e.Result=Engine.Analyze(input); };
            worker.RunWorkerCompleted+=(s,e)=>
            {
                busy=false;inputTabs.Enabled=true;tools.Enabled=true;menu.Enabled=true;UseWaitCursor=false;
                if(e.Error!=null) { status.Text="계산 실패";MessageBox.Show(this,e.Error.Message,"계산 오류");return; }
                calculated=input;result=(Analysis)e.Result;ShowResults();status.Text=string.Format("계산 완료 · {0:N2} s · 공칭 단면내력",(DateTime.UtcNow-start).TotalSeconds);log.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+status.Text+Environment.NewLine+string.Join(Environment.NewLine,result.Notes.ToArray())+Environment.NewLine);views.SelectedIndex=nextView;nextView=7;UpdateCaption();
            };worker.RunWorkerAsync();
        }
        void ShowResults()
        {
            foreach(Plot p in new[]{section,pmx,pmy,slice,surface}) { p.Project=calculated;p.Result=result;p.Invalidate(); }
            overview.Bind(calculated,result);reportSheet.Bind(calculated,result);
            summary.Text=string.Format("Ag {0:N0} mm² · As {1:N0} mm² · Pn {2:N1} ~ {3:N1} kN · 방향 비교 {4:P2}",result.GrossArea,result.SteelArea,result.PMin,result.PMax,result.AngularChange);
            foreach(DemandResult d in result.Demands)
            {
                string state=d.Status=="InsideNominal"?"내부":d.Status=="OutsideNominal"?"외부":"미확정";
                int i=checks.Rows.Add(d.Name,d.P.ToString("N2"),d.Mx.ToString("N2"),d.My.ToString("N2"),state,d.Utilization.HasValue?d.Utilization.Value.ToString("0.0000"):"—",d.Note);
                checks.Rows[i].DefaultCellStyle.ForeColor=d.Status=="InsideNominal"?Color.DarkGreen:d.Status=="OutsideNominal"?Color.Firebrick:Color.DarkOrange;
            }
            detail.Text="상관곡선의 점을 클릭하면 중립축·응력·변형률을 확인할 수 있습니다.\r\n\r\n"+string.Join("\r\n\r\n",result.Notes.ToArray());
        }
        void SelectPoint(CapacityPoint point)
        {
            if(result==null)return;CapacityPoint q=Engine.Evaluate(calculated,point.Angle,point.Depth<0?double.PositiveInfinity:point.Depth,true);
            foreach(Plot p in new[]{section,pmx,pmy,slice}) { p.Selected=q;p.Invalidate(); }
            overview.SelectPoint(q);
            double pTolerance=Math.Max(1e-5,Math.Abs(result.PMax-result.PMin)*1e-9);
            detail.Text=string.Format(CultureInfo.InvariantCulture,"공칭 단면 상태\r\nP = {0:F4} kN    Mx = {1:F4} kN·m    My = {2:F4} kN·m\r\n압축면 법선 θ = {3:F3}°    c = {4}\r\n블록 순면적 = {5:F3} mm²    콘크리트력 = {6:F3} kN\r\n철근력 합계 = {7:F3} kN\r\nεs,min = {8:G7} / εs,max = {9:G7}\r\n빨강: 압축 / 파랑: 인장 / 점선: 중립축\r\n{10}",q.P,q.Mx,q.My,q.Angle,q.Depth<0?"∞":F(q.Depth)+" mm",q.ConcreteArea,q.ConcreteForce,q.SteelForce,q.MinSteelStrain,q.MaxSteelStrain,q.State);
            steel.Rows.Clear();for(int i=0;i<q.Bars.Count;i++){BarState b=q.Bars[i];steel.Rows.Add(i+1,b.Strain.ToString("G7"),b.Stress.ToString("F3"),b.Force.ToString("F3"));}
            detail.AppendText("\r\n"+Engine.StrainState(q,calculated.Material)+"  (설계기준별 지배구간 판정과 별개)\r\n");
            detail.AppendText(Math.Abs(q.P)<=pTolerance?"P ≈ 0: e = M/P는 유한값으로 정의하지 않음":"ey = Mx/P = "+(q.Mx/q.P*1000).ToString("0.###")+" mm / ex = −My/P = "+(-q.My/q.P*1000).ToString("0.###")+" mm");
            status.Text="계산점 선택됨 · [단면 / 배근]과 [계산점 상세] 탭에서 확인";
        }
        bool MayReplace() { if(!dirty)return true;DialogResult r=MessageBox.Show(this,"변경한 프로젝트를 저장할까요?","프로젝트",MessageBoxButtons.YesNoCancel);if(r==DialogResult.Cancel)return false;if(r==DialogResult.Yes){Save();return !dirty;}return true; }
        void NewProject(Project p) { if(!MayReplace())return;Fill(p);filePath=null;workspace.BringToFront();status.Text="프로젝트를 불러왔습니다. 계산을 실행하세요."; }
        void Standard()
        {
            using(StandardForm f=new StandardForm())if(f.ShowDialog(this)==DialogResult.OK)NewProject(f.Project);
        }
        void Open() { if(!MayReplace())return;using(OpenFileDialog f=new OpenFileDialog{Filter="PM Studio 프로젝트 (*.pmproj.json)|*.pmproj.json|JSON (*.json)|*.json"})if(f.ShowDialog(this)==DialogResult.OK)OpenPath(f.FileName); }
        void OpenPath(string path) { Guard(()=> { if(new FileInfo(path).Length>2000000)throw new ArgumentException("프로젝트 파일이 2 MB를 초과합니다.");Project p=Files.Parse(File.ReadAllText(path,Encoding.UTF8));Fill(p);filePath=path;workspace.BringToFront();status.Text="프로젝트 열기 완료"; }); }
        void Save() { if(filePath==null) { SaveAs();return; }WriteProject(filePath); }
        void SaveAs() { using(SaveFileDialog f=new SaveFileDialog{Filter="PM Studio 프로젝트|*.pmproj.json",FileName="section.pmproj.json"})if(f.ShowDialog(this)==DialogResult.OK)WriteProject(f.FileName); }
        void WriteProject(string path) { Project p=Read();File.WriteAllText(path,Files.Json(p),new UTF8Encoding(false));filePath=path;project=p;dirty=false;UpdateCaption();status.Text="프로젝트 저장 완료"; }
        void NeedResult() { Read();if(result==null||calculated==null)throw new InvalidOperationException("입력을 적용한 뒤 먼저 계산해 주세요."); }
        void ExportCsv() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="상관면 CSV|*.csv",FileName="interaction.csv"})if(f.ShowDialog(this)==DialogResult.OK){Reports.Csv(f.FileName,calculated,result);status.Text="CSV 내보내기 완료";} }
        void ExportHtml() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="검토 HTML|*.html",FileName="interaction.html"})if(f.ShowDialog(this)==DialogResult.OK){Reports.Html(f.FileName,calculated,result);status.Text="HTML 내보내기 완료";} }
        void ExportImage() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="보고서 그림 PNG (300 dpi)|*.png",FileName="interaction-report.png"})if(f.ShowDialog(this)==DialogResult.OK){reportSheet.SaveImage(f.FileName,2400,2700);status.Text="보고서 상관도 PNG 저장 완료 · 2400×2700 / 300 dpi";} }
        void ExportDetailImage() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="선택 상관도 PNG (300 dpi)|*.png",FileName="interaction-detail.png"})if(f.ShowDialog(this)==DialogResult.OK){reportSheet.SaveDetailImage(f.FileName);status.Text="선택 상관도 PNG 저장 · 3000×1950 / 300 dpi";} }
        void ExportDetailMetafile() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="선택 상관도 벡터 EMF|*.emf",FileName="interaction-detail.emf"})if(f.ShowDialog(this)==DialogResult.OK){reportSheet.SaveDetailMetafile(f.FileName);status.Text="선택 상관도 벡터 EMF 저장";} }
        void ExportOverview() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="통합 탐색 그림 PNG|*.png",FileName="interaction-overview.png"})if(f.ShowDialog(this)==DialogResult.OK)overview.SaveImage(f.FileName,1800,1350); }
        void ExportTableImage() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="검토 요약표 PNG (300 dpi)|*.png",FileName="interaction-summary.png"})if(f.ShowDialog(this)==DialogResult.OK){reportSheet.SaveTableImage(f.FileName);status.Text="검토 요약표 PNG 저장 완료";} }
        void ExportSummaryCsv() { NeedResult();using(SaveFileDialog f=new SaveFileDialog{Filter="검토 요약표 CSV|*.csv",FileName="interaction-summary.csv"})if(f.ShowDialog(this)==DialogResult.OK){Reports.SummaryCsv(f.FileName,result);status.Text="검토 요약표 CSV 저장 완료";} }
        void ToggleApi()
        {
            if(api!=null) { api.Dispose();api=null;apiStatus.Text="API 중지";status.Text="로컬 API를 중지했습니다.";return; }
            api=new ApiServer(18741);try{api.Start();}catch{api.Dispose();api=null;throw;}apiStatus.Text="API 127.0.0.1:18741";status.Text="로컬 API 시작 · 현재 화면 입력과 독립된 요청별 계산";
        }
        void About() { MessageBox.Show(this,"PM Studio + 0.8\r\n\r\nRC 등가 직사각형 응력블록 + 탄성완전소성 철근\r\nmm / MPa / kN / kN·m, 압축 (+)\r\n\r\n공칭 단면내력과 명시적으로 선택한 ACI 계수 참고 비교를 제공합니다. 상세규정·장주효과·전단·좌굴·PSC는 미적용입니다.\r\nKDS 적합 설계판정은 후속 검증 범위입니다.\r\n\r\nMSEW+ 참고 이미지의 색상과 고전 Windows 구성을 참고한 독립 프로그램입니다.","모델 및 적용 범위"); }
        void ClosingApp(object s,FormClosingEventArgs e) { if(busy) { e.Cancel=true;return; }try{if(!MayReplace()){e.Cancel=true;return;}}catch(Exception ex){MessageBox.Show(ex.Message);e.Cancel=true;return;}if(api!=null)api.Dispose(); }
    }
}
