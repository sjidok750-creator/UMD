using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PMStudio
{
    public sealed class ReportSheet : UserControl
    {
        readonly Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.FromArgb(225,229,233)};
        readonly PictureBox paper=new PictureBox{BackColor=Color.White,SizeMode=PictureBoxSizeMode.StretchImage};
        readonly Label info=new Label{AutoSize=true,Padding=new Padding(8,7,0,0),Text="P최대 / |Mx|최대 / |My|최대 · 같은 조합의 동시 발생값"};
        readonly ReportDrawing drawing=new ReportDrawing();
        public readonly DataGridView SummaryGrid=new DataGridView{Dock=DockStyle.Top,Height=146,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false};
        public readonly ComboBox CaseSelector=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=125};
        public readonly ReportCaseCanvas CaseCanvas=new ReportCaseCanvas{Dock=DockStyle.Fill};
        readonly Button preview=new Button{Text="보고서 전체",AutoSize=true};
        Project project;Analysis result;bool fit,whole,previewDirty;readonly ToolTip tip=new ToolTip();
        public Action<CapacityPoint> Pick;
        public ReportSheet()
        {
            Font=new Font(Theme.FontName,9.5f);FlowLayoutPanel bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=38,WrapContents=false,BackColor=Theme.Background,Padding=new Padding(5,3,0,0)};
            Button width=new Button{Text="폭 맞춤",AutoSize=true},page=new Button{Text="한 장 보기",AutoSize=true};
            CaseSelector.Items.AddRange(new object[]{"P 최대 조합","|Mx| 최대 조합","|My| 최대 조합"});CaseSelector.SelectedIndex=0;
            CaseSelector.SelectedIndexChanged+=(s,e)=>{CaseCanvas.CaseIndex=CaseSelector.SelectedIndex;CaseCanvas.Invalidate();};
            preview.Click+=(s,e)=>{whole=!whole;ApplyView();};
            width.Click+=(s,e)=>{whole=true;fit=false;ApplyView();};page.Click+=(s,e)=>{whole=true;fit=true;ApplyView();};
            bar.Controls.Add(new Label{Text="검토조합",AutoSize=true,Padding=new Padding(0,5,0,0)});bar.Controls.Add(CaseSelector);bar.Controls.Add(preview);bar.Controls.Add(width);bar.Controls.Add(page);
            foreach(string name in new[]{"구분","하중조합","요구 Mu\n(kN·m)","공칭 Mn\n(kN·m)","Mn / Mu","판정"})SummaryGrid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=name,SortMode=DataGridViewColumnSortMode.NotSortable,MinimumWidth=70});
            SummaryGrid.Columns[0].FillWeight=102;SummaryGrid.Columns[1].FillWeight=130;SummaryGrid.Columns[2].FillWeight=105;SummaryGrid.Columns[3].FillWeight=115;SummaryGrid.Columns[4].FillWeight=90;SummaryGrid.Columns[5].FillWeight=110;
            SummaryGrid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(237,242,239);SummaryGrid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;SummaryGrid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;SummaryGrid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;SummaryGrid.ColumnHeadersHeight=46;SummaryGrid.RowTemplate.Height=32;
            SummaryGrid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;SummaryGrid.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;SummaryGrid.Columns[1].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;
            scroll.Controls.Add(paper);Controls.Add(scroll);Controls.Add(CaseCanvas);Controls.Add(SummaryGrid);Controls.Add(bar);scroll.Resize+=(s,e)=>LayoutPaper();CaseCanvas.Pick=q=>{if(Pick!=null)Pick(q);};
            SummaryGrid.SelectionChanged+=(s,e)=>{if(SummaryGrid.CurrentRow!=null&&SummaryGrid.CurrentRow.Index<CaseSelector.Items.Count)CaseSelector.SelectedIndex=SummaryGrid.CurrentRow.Index;};ApplyView();
            paper.MouseMove+=(s,e)=>{CapacityPoint q=Hit(e.Location);tip.SetToolTip(paper,q==null?"":string.Format("공칭 단면 상태 (계수 적용 전)\nP {0:N3} kN / Mx {1:N3} / My {2:N3} kN·m\n압축면 법선 {3:0.###}° / c {4}",q.P,q.Mx,q.My,q.Angle,q.Depth<0?"∞":q.Depth.ToString("0.###")+" mm"));};
            paper.MouseClick+=(s,e)=>{CapacityPoint q=Hit(e.Location);if(q!=null&&Pick!=null)Pick(q);};
        }
        void ApplyView(){scroll.Visible=whole;CaseCanvas.Visible=!whole;preview.Text=whole?"상세 그래프":"보고서 전체";if(whole){EnsurePreview();scroll.BringToFront();LayoutPaper();}else CaseCanvas.BringToFront();}
        void EnsurePreview(){if(previewDirty&&project!=null&&result!=null){paper.Image=drawing.Create(project,result,1800,2025);previewDirty=false;}}
        CapacityPoint Hit(Point p){return paper.Width<1?null:drawing.Hit(p.X*1200f/paper.Width,p.Y*1350f/paper.Height);}
        void LayoutPaper()
        {
            int width=Math.Max(350,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-24);
            if(fit)width=Math.Min(width,Math.Max(280,(scroll.ClientSize.Height-24)*1200/1350));
            paper.SetBounds(12,12,width,width*1350/1200);
        }
        public void Bind(Project p,Analysis r)
        {
            project=p;result=r;Image old=paper.Image;paper.Image=null;if(old!=null)old.Dispose();
            SummaryGrid.Rows.Clear();if(r!=null)foreach(ReportRow row in Reports.Summary(r))
            {
                bool reduced=p.ReductionProfile!="Nominal";double? capacity=reduced?row.ReducedMn:row.NominalMn,ratio=reduced?row.ReducedCapacityDemandRatio:row.NominalCapacityDemandRatio;string state=reduced?row.ReducedStatus:row.Status;
                SummaryGrid.Columns[3].HeaderText=reduced?"계수 φMn\n(kN·m)":"공칭 Mn\n(kN·m)";SummaryGrid.Columns[4].HeaderText=reduced?"φMn / Mu":"Mn / Mu";
                int i=SummaryGrid.Rows.Add(row.Division,row.Combination,row.Mu.ToString("N2"),capacity.HasValue?capacity.Value.ToString("N2"):"—",ratio.HasValue?ratio.Value.ToString("0.0000"):"—",state);
                SummaryGrid.Rows[i].DefaultCellStyle.ForeColor=state.StartsWith("N.G.")?Color.Firebrick:state.StartsWith("O.K.")?Color.DarkGreen:Color.DarkOrange;
                foreach(DataGridViewCell cell in SummaryGrid.Rows[i].Cells)cell.ToolTipText=row.Note;
            }
            using(Graphics metrics=CreateGraphics())
            {
                float scale=metrics.DpiX/96;SummaryGrid.RowTemplate.Height=(int)(29*scale);foreach(DataGridViewRow row in SummaryGrid.Rows)row.Height=SummaryGrid.RowTemplate.Height;
                int header=(int)Math.Ceiling(SummaryGrid.Font.GetHeight(metrics)*2+14*scale);SummaryGrid.ColumnHeadersHeight=header;SummaryGrid.Height=header+3*SummaryGrid.RowTemplate.Height+(int)(6*scale);
            }
            CaseCanvas.Project=p;CaseCanvas.Result=r;CaseCanvas.CaseIndex=0;CaseSelector.SelectedIndex=0;CaseCanvas.Invalidate();
            previewDirty=p!=null&&r!=null;drawing.ClearHits();if(whole)EnsurePreview();LayoutPaper();
        }
        public void SaveImage(string path,int width,int height)
        {
            if(project==null||result==null)throw new InvalidOperationException("계산 결과가 없습니다.");
            if(width<1200||height<1350||Math.Abs((double)width/height-1200.0/1350)>0.001)throw new ArgumentException("보고서 그림은 1200:1350 비율, 폭 1200 px 이상입니다.");
            using(ReportDrawing export=new ReportDrawing())using(Bitmap image=export.Create(project,result,width,height))image.Save(path,ImageFormat.Png);
        }
        public void SaveTableImage(string path)
        {
            if(result==null)throw new InvalidOperationException("계산 결과가 없습니다.");
            using(ReportDrawing export=new ReportDrawing())using(Bitmap image=export.CreateTable(result))image.Save(path,ImageFormat.Png);
        }
        public void SaveDetailImage(string path)
        {
            if(project==null||result==null||result.ReportDiagrams.Count==0)throw new InvalidOperationException("계산 결과가 없습니다.");
            using(Bitmap image=new Bitmap(3000,1950))using(Graphics g=Graphics.FromImage(image))using(ReportDrawing export=new ReportDrawing())
            {image.SetResolution(300,300);g.Clear(Color.White);g.ScaleTransform(3,3);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;export.DrawCase(g,project,result,CaseCanvas.CaseIndex,1000,650);image.Save(path,ImageFormat.Png);}
        }
        public void SaveDetailMetafile(string path)
        {
            if(project==null||result==null||result.ReportDiagrams.Count==0)throw new InvalidOperationException("계산 결과가 없습니다.");
            using(Bitmap reference=new Bitmap(1,1))using(Graphics device=Graphics.FromImage(reference))
            {
                IntPtr hdc=device.GetHdc();try
                {
                    // Automatic vector bounds avoid a device-DPI frame mismatch on high-DPI Windows.
                    using(Metafile file=new Metafile(path,hdc,EmfType.EmfPlusDual))using(Graphics g=Graphics.FromImage(file))using(ReportDrawing export=new ReportDrawing())
                    {g.PageUnit=GraphicsUnit.Pixel;g.FillRectangle(Brushes.White,0,0,1000,650);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;export.DrawCase(g,project,result,CaseCanvas.CaseIndex,1000,650);}
                }finally{device.ReleaseHdc(hdc);}
            }
        }
        protected override void Dispose(bool disposing){if(disposing){if(paper.Image!=null)paper.Image.Dispose();tip.Dispose();drawing.Dispose();}base.Dispose(disposing);}
    }
    public sealed class ReportCaseCanvas : Control
    {
        public Project Project;public Analysis Result;public int CaseIndex;public Action<CapacityPoint> Pick;
        readonly ReportDrawing drawing=new ReportDrawing();readonly ToolTip tip=new ToolTip();float dpi=1;
        public ReportCaseCanvas(){DoubleBuffered=true;BackColor=Color.White;SetStyle(ControlStyles.ResizeRedraw,true);}
        public void Render(Graphics g,int width,int height)
        {
            GraphicsState state=g.Save();try{g.Clear(Color.White);dpi=g.DpiX/96;g.ScaleTransform(dpi,dpi);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            if(Project!=null&&Result!=null&&Result.ReportDiagrams.Count>CaseIndex)drawing.DrawCase(g,Project,Result,CaseIndex,width/dpi,height/dpi);
            else using(Font font=Theme.Font(14,FontStyle.Regular))g.DrawString("입력 후 계산(F5)을 실행하세요.",font,Brushes.DimGray,24,25);
            }finally{g.Restore(state);}
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Render(e.Graphics,Width,Height);}
        protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);CapacityPoint q=drawing.Hit(e.X/dpi,e.Y/dpi);if(q!=null&&Pick!=null)Pick(q);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);CapacityPoint q=drawing.Hit(e.X/dpi,e.Y/dpi);tip.SetToolTip(this,q==null?"":"공칭 상태 · P "+q.P.ToString("N3")+" kN · Mx "+q.Mx.ToString("N3")+" / My "+q.My.ToString("N3")+" kN·m");}
        protected override void Dispose(bool disposing){if(disposing){drawing.Dispose();tip.Dispose();}base.Dispose(disposing);}
    }
    public sealed class PlotAnnotation
    {public string Text,Kind;public RectangleF Bounds;public PointF Anchor;}
    public sealed class PlotLayout
    {public RectangleF Plot,Legend,Values;public readonly List<PlotAnnotation> Text=new List<PlotAnnotation>();public readonly List<PointF> Curve=new List<PointF>();public readonly List<PointF[]> Paths=new List<PointF[]>();}
    public sealed class ReportDrawing : IDisposable
    {
        readonly Font title=Theme.Font(23,FontStyle.Bold),heading=Theme.Font(16,FontStyle.Bold),body=Theme.Font(13,FontStyle.Regular),small=Theme.Font(12,FontStyle.Regular),strong=Theme.Font(13,FontStyle.Bold);
        readonly List<KeyValuePair<PointF,CapacityPoint>> hits=new List<KeyValuePair<PointF,CapacityPoint>>();
        static readonly Color Curve=Color.FromArgb(32,38,44),Grid=Color.FromArgb(215,222,229),Red=Color.FromArgb(192,59,49),Blue=Color.FromArgb(37,89,163);
        public void Dispose(){title.Dispose();heading.Dispose();body.Dispose();small.Dispose();strong.Dispose();}
        readonly List<PlotLayout> layouts=new List<PlotLayout>();PlotLayout activeLayout;
        public IList<PlotLayout> Layouts {get{return layouts.AsReadOnly();}}
        public void ClearHits(){hits.Clear();layouts.Clear();}
        public CapacityPoint Hit(float x,float y)
        {
            double best=144;CapacityPoint found=null;foreach(var h in hits){double dx=h.Key.X-x,dy=h.Key.Y-y,d=dx*dx+dy*dy;if(d<best){best=d;found=h.Value;}}return found;
        }
        static string N(double v){return v.ToString(Math.Abs(v)>=1e7?"0.###E+0":"#,0.00",CultureInfo.InvariantCulture);}
        static double M(CapacityPoint q){return Math.Sqrt(q.Mx*q.Mx+q.My*q.My);}
        static string Pair(CapacityPoint q){return q==null?"평형점 없음":"("+N(M(q))+", "+N(q.P)+")";}
        void Text(Graphics g,string s,float x,float y,Font f,Color color){using(Brush b=new SolidBrush(color))g.DrawString(s,f,b,x,y);}
        void BoxText(Graphics g,string s,float x,float y,Color color)
        {SizeF z=g.MeasureString(s,small);using(Brush white=new SolidBrush(Color.FromArgb(235,255,255,255)))g.FillRectangle(white,x-2,y,z.Width+4,z.Height);Text(g,s,x,y,small,color);}
        void Center(Graphics g,string s,float x,float y,Font f,Color c){Text(g,s,x-g.MeasureString(s,f).Width/2,y,f,c);}
        public Bitmap Create(Project p,Analysis r,int width,int height)
        {
            Bitmap image=new Bitmap(width,height);image.SetResolution(300,300);
            using(Graphics g=Graphics.FromImage(image))
            {g.Clear(Color.White);g.ScaleTransform(width/1200f,height/1350f);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;Draw(g,p,r);}
            return image;
        }
        public Bitmap CreateTable(Analysis r)
        {
            Bitmap image=new Bitmap(2400,740);image.SetResolution(300,300);
            using(Graphics g=Graphics.FromImage(image))
            {
                g.Clear(Color.White);g.ScaleTransform(2,2);g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                Text(g,"교각 P–M 상관도 검토 결과",30,20,title,Theme.Ink);Table(g,r,new RectangleF(30,70,1140,211),false);
                Text(g,"Mu = √(Mx²+My²) · 같은 조합의 P, Mx, My 사용 · 동일 축력에서 방향별 Mn 산정",32,300,body,Theme.Muted);
                Text(g,r.ReportDiagrams.Any(d=>d.ReductionProfile!="Nominal")?Reduction.Label(r.ReportDiagrams[0].ReductionProfile)+" · 부재 전체 설계판정 아님":"공칭내력 기준. 강도감소계수 및 축력상한 미적용. Mn/Mu는 φMn/Mu 설계판정과 다릅니다.",32,325,body,Theme.Muted);
            }
            return image;
        }
        void Table(Graphics g,Analysis r,RectangleF rect,bool compact)
        {
            float[] widths=compact?new[]{0.24f,0.22f,0.22f,0.13f,0.19f}:new[]{0.24f,0.22f,0.22f,0.13f,0.19f};
            bool reduced=r.ReportDiagrams.Any(d=>d.ReductionProfile!="Nominal");string[] header={"Division","Demand Mu\n(kN·m)",reduced?"Capacity φMn\n참고 (kN·m)":"Capacity Mn\n공칭 (kN·m)",reduced?"φMn/Mu":"Mn/Mu","Remark"};
            float head=compact?42:57,rowHeight=(rect.Height-head)/3;
            using(Pen line=new Pen(Theme.Ink,1.5f))g.DrawLine(line,rect.Left,rect.Top,rect.Right,rect.Top);
            using(Pen line=new Pen(Theme.Border,0.7f))
            {
                g.DrawLine(line,rect.Left,rect.Top+head,rect.Right,rect.Top+head);
                for(int i=1;i<3;i++)g.DrawLine(line,rect.Left,rect.Top+head+i*rowHeight,rect.Right,rect.Top+head+i*rowHeight);
            }
            using(Pen line=new Pen(Theme.Ink,1.5f))g.DrawLine(line,rect.Left,rect.Bottom,rect.Right,rect.Bottom);
            float x=rect.X;for(int i=0;i<5;i++){Center(g,header[i],x+widths[i]*rect.Width/2,rect.Y+8,compact?small:strong,Theme.Ink);x+=widths[i]*rect.Width;}
            List<ReportRow> rows=Reports.Summary(r);
            for(int k=0;k<rows.Count;k++)
            {
                ReportRow row=rows[k];double? capacity=reduced?row.ReducedMn:row.NominalMn,ratio=reduced?row.ReducedCapacityDemandRatio:row.NominalCapacityDemandRatio;string status=reduced?row.ReducedStatus:row.Status;string[] text={row.Division,N(row.Mu),capacity.HasValue?N(capacity.Value):"—",ratio.HasValue?ratio.Value.ToString("0.0000"):"—",status.Replace(" (공칭)","").Replace(" (참고)","")};
                x=rect.X;for(int i=0;i<5;i++){Center(g,text[i],x+widths[i]*rect.Width/2,rect.Y+head+k*rowHeight+(rowHeight-16)/2,compact?small:strong,i==4&&status.StartsWith("N.G.")?Red:Theme.Ink);x+=widths[i]*rect.Width;}
            }
        }
        public void Draw(Graphics g,Project p,Analysis r)
        {
            hits.Clear();layouts.Clear();Text(g,"P–M INTERACTION DIAGRAM",30,21,title,Theme.Ink);using(Brush ink=new SolidBrush(Theme.Ink))using(StringFormat format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(p.Name,heading,ink,new RectangleF(32,54,1136,25),format);
            bool reduced=p.ReductionProfile!="Nominal";
            Text(g,(reduced?Reduction.Label(p.ReductionProfile)+" (현재 KDS 판정 아님)":"RC 공칭 단면내력")+"  |  압축 P (+)  |  mm · MPa · kN · kN·m",32,80,body,Theme.Muted);
            using(Pen pen=new Pen(Theme.Ink,1))g.DrawLine(pen,30,105,1170,105);
            Section(g,p,r,new RectangleF(20,119,570,570));
            RectangleF[] cells={new RectangleF(610,119,570,570),new RectangleF(20,714,570,570),new RectangleF(610,714,570,570)};
            for(int i=0;i<3;i++)
            {
                if(r.ReportDiagrams.Count>i)Diagram(g,r,r.ReportDiagrams[i],cells[i]);
                else Text(g,"하중조합을 입력하고 다시 계산하세요.",cells[i].X+20,cells[i].Y+100,body,Theme.Muted);
            }
            using(Pen pen=new Pen(Theme.Border,0.8f))g.DrawLine(pen,30,1304,1170,1304);

        }
        public void DrawCase(Graphics g,Project p,Analysis r,int index,float width,float height)
        {
            hits.Clear();layouts.Clear();DirectionalDiagram d=r.ReportDiagrams[index];float side=width>=720?185:0;
            Diagram(g,r,d,new RectangleF(0,0,width-side,height),true);
            if(side==0)return;
            float x=width-side+8,sideTop=58,sideShift=sideTop-11;using(Pen pen=new Pen(Theme.Border,0.8f))g.DrawLine(pen,x-7,sideTop,x-7,height-12);
            Text(g,"단면 / 배근",x+7,sideTop,strong,Theme.Ink);Point2[] vertices=p.Contours.SelectMany(c=>c.Points).ToArray();double minx=vertices.Min(q=>q.X),maxx=vertices.Max(q=>q.X),miny=vertices.Min(q=>q.Y),maxy=vertices.Max(q=>q.Y);
            double scale=Math.Min((side-35)/(maxx-minx),145/(maxy-miny));Func<double,double,PointF> tr=(a,b)=>new PointF(x+(side-16)/2+(float)((a-(minx+maxx)/2)*scale),110+sideShift-(float)((b-(miny+maxy)/2)*scale));
            using(GraphicsPath path=new GraphicsPath(FillMode.Alternate))using(Brush fill=new SolidBrush(Color.FromArgb(248,224,191)))using(Pen border=new Pen(Theme.Ink,1)){foreach(Contour c in p.Contours)path.AddPolygon(c.Points.Select(q=>tr(q.X,q.Y)).ToArray());g.FillPath(fill,path);g.DrawPath(border,path);}
            foreach(Rebar b in p.Bars){PointF q=tr(b.X,b.Y);float size=(float)Math.Max(2.5,2*Math.Sqrt(b.Area/Math.PI)*scale);g.FillEllipse(Brushes.Black,q.X-size/2,q.Y-size/2,size,size);}
            List<string> lines=new List<string>{"B×H "+N(maxx-minx)+"×"+N(maxy-miny),"단위 mm","철근 "+p.Bars.Count+"개", "As "+N(r.SteelArea)+" mm²","", "평형점 B"};
            if(d.Balanced!=null){lines.Add("Pb "+N(d.Balanced.P)+" kN");lines.Add("Mb "+N(M(d.Balanced))+" kN·m");lines.Add("eb "+N(1000*M(d.Balanced)/d.Balanced.P)+" mm");lines.Add("cb "+N(d.Balanced.Depth)+" mm");lines.Add("εt "+(-d.Balanced.MinSteelStrain).ToString("0.000000"));}
            lines.Add("");lines.Add("M₀ "+(d.PureBending==null?"—":N(M(d.PureBending)))+" kN·m");lines.Add(d.SolvedPoints+"개 평형 계산점");
            if(p.ReductionProfile=="Reference-KR2008-Tied"){lines.RemoveAt(lines.Count-1);lines.Add("원문 B 정의 대조 보류");lines.Add("최외단 항복 기준 사용");}
            float top=190+sideShift;foreach(string line in lines){Text(g,line,x+4,top,small,Theme.Muted);top+=18;}
        }
        void Section(Graphics g,Project p,Analysis r,RectangleF cell)
        {
            float x=cell.X,y=cell.Y;Text(g,"(a) 단면 및 배근",x+18,y+5,heading,Theme.Ink);
            Point2[] pts=p.Contours.SelectMany(c=>c.Points).ToArray();double minx=pts.Min(q=>q.X),maxx=pts.Max(q=>q.X),miny=pts.Min(q=>q.Y),maxy=pts.Max(q=>q.Y);
            RectangleF viewport=new RectangleF(x+70,y+59,cell.Width-137,182);double scale=Math.Min(viewport.Width/(maxx-minx),viewport.Height/(maxy-miny));
            Func<double,double,PointF> tr=(a,b)=>new PointF(viewport.X+viewport.Width/2+(float)((a-(minx+maxx)/2)*scale),viewport.Y+viewport.Height/2-(float)((b-(miny+maxy)/2)*scale));
            using(GraphicsPath path=new GraphicsPath(FillMode.Alternate))using(Pen edge=new Pen(Color.FromArgb(60,60,60),1.4f))using(Brush fill=new SolidBrush(Color.FromArgb(248,224,191)))
            {foreach(Contour c in p.Contours)path.AddPolygon(c.Points.Select(q=>tr(q.X,q.Y)).ToArray());g.FillPath(fill,path);g.DrawPath(edge,path);}
            foreach(Rebar b in p.Bars){PointF z=tr(b.X,b.Y);float d=(float)Math.Max(2.5,2*Math.Sqrt(b.Area/Math.PI)*scale);g.FillEllipse(Brushes.Black,z.X-d/2,z.Y-d/2,d,d);}
            PointF tl=tr(minx,maxy),br=tr(maxx,miny);float dy=br.Y+18,dx=tl.X-20;
            using(Pen pen=new Pen(Theme.Muted,0.7f))
            {
                g.DrawLine(pen,tl.X,br.Y+3,tl.X,dy+5);g.DrawLine(pen,br.X,br.Y+3,br.X,dy+5);g.DrawLine(pen,tl.X,dy,br.X,dy);
                g.DrawLine(pen,tl.X-4,dy+4,tl.X+4,dy-4);g.DrawLine(pen,br.X-4,dy+4,br.X+4,dy-4);
                g.DrawLine(pen,dx,tl.Y,dx,br.Y);g.DrawLine(pen,dx-4,tl.Y+4,dx+4,tl.Y-4);g.DrawLine(pen,dx-4,br.Y+4,dx+4,br.Y-4);
                g.DrawLine(pen,dx-5,tl.Y,tl.X-3,tl.Y);g.DrawLine(pen,dx-5,br.Y,tl.X-3,br.Y);
            }
            Center(g,N(maxx-minx)+" mm",(tl.X+br.X)/2,dy+3,body,Theme.Ink);
            GraphicsState saved=g.Save();g.TranslateTransform(dx-21,(tl.Y+br.Y)/2);g.RotateTransform(-90);Center(g,N(maxy-miny)+" mm",0,-8,body,Theme.Ink);g.Restore(saved);
            PointF origin=tr(r.Cx,r.Cy);using(Pen axes=new Pen(Color.FromArgb(130,130,130),0.7f)){axes.DashStyle=DashStyle.Dash;g.DrawLine(axes,origin.X-24,origin.Y,origin.X+35,origin.Y);g.DrawLine(axes,origin.X,origin.Y+24,origin.X,origin.Y-35);}
            Text(g,"X",origin.X+35,origin.Y-5,small,Theme.Muted);Text(g,"Y",origin.X-5,origin.Y-50,small,Theme.Muted);
            float tab=y+290;
            Text(g,"재료 및 단면",x+35,tab,strong,Theme.Ink);
            string[] rows={"f′c "+N(p.Material.Fc)+" / fy "+N(p.Material.Fy)+" MPa · εcu "+p.Material.EpsCu.ToString("0.#####")+" · 철근 "+p.Bars.Count+"개",
                "Ag "+N(r.GrossArea)+" mm²   As "+N(r.SteelArea)+" mm²",
                "P₀ "+N(r.PMax)+" / Pt "+N(r.PMin)+" kN · α "+p.Material.Alpha.ToString("0.###")+" / β "+p.Material.Beta.ToString("0.###")};
            for(int i=0;i<rows.Length;i++)Text(g,rows[i],x+35,tab+23+i*20,body,Theme.Ink);
            Table(g,r,new RectangleF(x+18,tab+98,cell.Width-36,143),true);
            Text(g,"Mu = √(Mx²+My²) · 동일 조합의 P, Mx, My 사용",x+25,tab+250,small,Theme.Muted);
            Text(g,p.ReductionProfile=="Nominal"?"Mn/Mu 공칭 검토 · φMn/Mu는 기준 확정 후 산정":Reduction.Label(p.ReductionProfile)+" · 부재 전체 판정 아님",x+25,tab+267,small,Theme.Muted);
        }
        void Key(Graphics g,string label,float x,float y,Color color,DashStyle style,float width)
        {using(Pen pen=new Pen(color,width)){pen.DashStyle=style;g.DrawLine(pen,x,y+8,x+27,y+8);}Text(g,label,x+34,y,small,color);}
        readonly List<RectangleF> labelBounds=new List<RectangleF>();
        void Callout(Graphics g,string label,PointF point,RectangleF chart,Color color,int preferred)
        {
            SizeF measured=g.MeasureString(label,small);RectangleF chosen=RectangleF.Empty;
            PointF[] offsets={new PointF(11,-27),new PointF(11,10),new PointF(-measured.Width-12,-27),new PointF(-measured.Width-12,10),new PointF(11,-46),new PointF(-measured.Width-12,29),new PointF(24,-63),new PointF(-measured.Width-24,44)};
            for(int i=0;i<offsets.Length;i++)
            {
                PointF off=offsets[(i+preferred)%offsets.Length];RectangleF box=new RectangleF(Math.Max(chart.Left+3,Math.Min(chart.Right-measured.Width-7,point.X+off.X)),Math.Max(chart.Top+3,Math.Min(chart.Bottom-measured.Height-5,point.Y+off.Y)),measured.Width+4,measured.Height+2);
                if(!labelBounds.Any(b=>b.IntersectsWith(box))&&!hits.Any(h=>box.Contains(h.Key))&&!CurveHits(box)){chosen=box;break;}
            }
            if(chosen.IsEmpty)return;labelBounds.Add(chosen);if(activeLayout!=null)activeLayout.Text.Add(new PlotAnnotation{Text=label,Kind="point",Bounds=chosen,Anchor=point});
            using(Pen leader=new Pen(Color.FromArgb(140,color),.65f))g.DrawLine(leader,point,new PointF(Math.Max(chosen.Left,Math.Min(chosen.Right,point.X)),Math.Max(chosen.Top,Math.Min(chosen.Bottom,point.Y))));
            Text(g,label,chosen.X+2,chosen.Y,small,color);
        }
        bool CurveHits(RectangleF box)
        {
            if(activeLayout==null)return false;
            RectangleF padded=box;padded.Inflate(2,2);
            foreach(PointF[] path in activeLayout.Paths)for(int i=1;i<path.Length;i++)if(SegmentIntersects(path[i-1],path[i],padded))return true;
            return false;
        }
        public static bool SegmentIntersects(PointF a,PointF b,RectangleF box)
        {
            double lo=0,hi=1;double[] start={a.X,a.Y},delta={b.X-a.X,b.Y-a.Y},min={box.Left,box.Top},max={box.Right,box.Bottom};
            for(int axis=0;axis<2;axis++)
            {
                if(Math.Abs(delta[axis])<1e-10){if(start[axis]<min[axis]||start[axis]>max[axis])return false;}
                else {double p=(min[axis]-start[axis])/delta[axis],q=(max[axis]-start[axis])/delta[axis];lo=Math.Max(lo,Math.Min(p,q));hi=Math.Min(hi,Math.Max(p,q));if(lo>hi)return false;}
            }
            return true;
        }
        void Inset(Graphics g,RectangleF box,string caption,bool values)
        {
            using(Brush paper=new SolidBrush(Color.White))g.FillRectangle(paper,box);
            using(Pen edge=new Pen(Color.FromArgb(198,208,219),.7f))g.DrawRectangle(edge,box.X,box.Y,box.Width,box.Height);
            CardText(g,caption,box,8,strong,Theme.Ink,values?"value":"legend");
            using(Pen line=new Pen(Color.FromArgb(230,235,240),.6f))g.DrawLine(line,box.Left+10,box.Top+27,box.Right-10,box.Top+27);
        }
        void CardText(Graphics g,string text,RectangleF card,float offset,Font font,Color color,string kind)
        {
            float left=card.Left+11;SizeF measured=g.MeasureString(text,font);RectangleF bounds=new RectangleF(left,card.Top+offset,measured.Width,measured.Height);
            if(activeLayout!=null)activeLayout.Text.Add(new PlotAnnotation{Text=text,Kind=kind,Bounds=bounds});
            Text(g,text,bounds.X,bounds.Y,font,color);
        }
        void InsetKey(Graphics g,string text,RectangleF card,float offset,Color color,DashStyle style,float weight)
        {
            using(Pen pen=new Pen(color,weight)){pen.DashStyle=style;g.DrawLine(pen,card.Left+11,card.Top+offset+7,card.Left+32,card.Top+offset+7);}
            float left=card.Left+39;SizeF measured=g.MeasureString(text,small);RectangleF bounds=new RectangleF(left,card.Top+offset,measured.Width,measured.Height);
            if(activeLayout!=null)activeLayout.Text.Add(new PlotAnnotation{Text=text,Kind="legend",Bounds=bounds});Text(g,text,bounds.X,bounds.Y,small,color);
        }
        static double RayEnd(DirectionalDiagram d,double moment,double axial)
        {
            double best=double.PositiveInfinity;
            IEnumerable<PointF[]> paths=d.ReductionProfile=="Nominal"?d.Paths.Select(path=>path.Select(q=>new PointF((float)M(q),(float)q.P)).ToArray()):d.ReducedPaths.Select(path=>path.Select(q=>new PointF((float)Math.Sqrt(q.Mx*q.Mx+q.My*q.My),(float)q.P)).ToArray());
            foreach(PointF[] path in paths)for(int i=1;i<path.Length;i++)
            {
                double ax=path[i-1].X,ay=path[i-1].Y,dx=path[i].X-ax,dy=path[i].Y-ay,den=moment*dy-axial*dx;if(Math.Abs(den)<1e-12)continue;
                double lambda=(ax*dy-ay*dx)/den,t=(ax*axial-ay*moment)/den;
                if(lambda>1e-9&&t>=-1e-8&&t<=1+1e-8)best=Math.Min(best,lambda);
            }
            // Visual construction line only. Capacity evaluation uses solved equilibrium states.
            return double.IsInfinity(best)?1:Math.Max(1,best);
        }
        void Diagram(Graphics g,Analysis result,DirectionalDiagram d,RectangleF cell,bool screen=false)
        {
            float x=cell.X,y=cell.Y;labelBounds.Clear();Text(g,d.Title,x+17,y+5,heading,Theme.Ink);
            using(Brush ink=new SolidBrush(Theme.Muted))using(StringFormat format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(d.Load.Name,body,ink,new RectangleF(x+18,y+33,Math.Max(100,cell.Width-180),20),format);
            bool reduced=d.ReductionProfile!="Nominal";
            List<CapacityPoint> points=d.Paths.SelectMany(q=>q).ToList();if(points.Count==0){Text(g,"이 방향에서 평형 곡선을 찾지 못했습니다.",x+30,y+140,body,Red);return;}
            double md=Math.Sqrt(d.Load.Mx*d.Load.Mx+d.Load.My*d.Load.My);
            RectangleF chart=new RectangleF(x+77,y+82,cell.Width-105,Math.Max(210,cell.Height-126));
            float inset=12,gap=12,cardHeight=134,valuesWidth=Math.Min(235,(chart.Width-2*inset-gap)*.53f);
            RectangleF legend=new RectangleF(chart.Left+inset,chart.Top+inset,chart.Width-2*inset-gap-valuesWidth,cardHeight);
            RectangleF values=new RectangleF(legend.Right+gap,legend.Top,valuesWidth,cardHeight);
            AxisRange ax=AxisRange.Nice(0,Math.Max(points.Max(q=>M(q)),md)*1.09,6);
            double peak=Math.Max(points.Max(q=>q.P),Math.Max(result.PMax,d.Load.P));
            if(reduced)peak=Math.Max(peak,d.ReducedPaths.SelectMany(path=>path).Select(q=>q.P).DefaultIfEmpty(peak).Max());
            AxisRange ap=AxisRange.Nice(Math.Min(result.PMin,d.Load.P),peak*1.035,9);
            // Reserve real coordinate headroom above every plotted state; cards never mask curves.
            double fraction=(cardHeight+2*inset+12)/chart.Height;
            ap=AxisRange.Nice(ap.Min,ap.Min+(peak-ap.Min)/Math.Max(.2,1-fraction),9);
            activeLayout=new PlotLayout{Plot=chart,Legend=legend,Values=values};layouts.Add(activeLayout);
            labelBounds.Add(legend);labelBounds.Add(values);
            Func<double,double,PointF> tr=(m,pn)=>new PointF(chart.Left+(float)ax.Unit(m)*chart.Width,chart.Bottom-(float)ap.Unit(pn)*chart.Height);
            using(Pen minor=new Pen(Color.FromArgb(238,241,245),.5f))using(Pen grid=new Pen(Grid,.7f))using(Pen border=new Pen(Theme.Ink,.9f))
            {
                double[] xt=ax.Ticks().ToArray(),yt=ap.Ticks().ToArray();
                for(int i=1;i<xt.Length;i++){float a=tr((xt[i-1]+xt[i])/2,0).X;g.DrawLine(minor,a,chart.Top,a,chart.Bottom);}
                for(int i=1;i<yt.Length;i++){float a=tr(0,(yt[i-1]+yt[i])/2).Y;g.DrawLine(minor,chart.Left,a,chart.Right,a);}
                foreach(double v in xt){float a=tr(v,0).X;g.DrawLine(grid,a,chart.Top,a,chart.Bottom);g.DrawLine(border,a,chart.Bottom,a,chart.Bottom+4);Center(g,ax.Text(v),a,chart.Bottom+8,small,Theme.Ink);}
                foreach(double v in yt){float b=tr(0,v).Y;g.DrawLine(grid,chart.Left,b,chart.Right,b);g.DrawLine(border,chart.Left-4,b,chart.Left,b);string text=ap.Text(v);Text(g,text,chart.Left-g.MeasureString(text,small).Width-9,b-8,small,Theme.Ink);}
                g.DrawRectangle(border,chart.X,chart.Y,chart.Width,chart.Height);
                if(ap.Min<0&&ap.Max>0)g.DrawLine(border,chart.Left,tr(0,0).Y,chart.Right,tr(0,0).Y);
            }
            Text(g,"축력 P (kN)",chart.Left-2,chart.Top-24,strong,Theme.Ink);Text(g,"모멘트 M (kN·m)",chart.Right-123,chart.Bottom+29,strong,Theme.Ink);
            GraphicsState clip=g.Save();g.SetClip(chart);
            if(reduced)using(Brush fill=new SolidBrush(Color.FromArgb(10,Blue)))foreach(var path in d.ReducedPaths)
            {if(path.Count>3&&Math.Sqrt(path[0].Mx*path[0].Mx+path[0].My*path[0].My)<1e-5&&Math.Sqrt(path.Last().Mx*path.Last().Mx+path.Last().My*path.Last().My)<1e-5)g.FillPolygon(fill,path.Select(q=>tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P)).ToArray());}
            foreach(var path in d.Paths)activeLayout.Paths.Add(path.Select(q=>tr(M(q),q.P)).ToArray());
            if(reduced)foreach(var path in d.ReducedPaths)activeLayout.Paths.Add(path.Select(q=>tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P)).ToArray());
            using(Pen curve=new Pen(Curve,2))foreach(List<CapacityPoint> path in d.Paths)
            {if(path.Count>1)g.DrawLines(curve,path.Select(q=>tr(M(q),q.P)).ToArray());foreach(CapacityPoint q in path){PointF mapped=tr(M(q),q.P);hits.Add(new KeyValuePair<PointF,CapacityPoint>(mapped,q));activeLayout.Curve.Add(mapped);}}
            if(reduced)using(Pen curve=new Pen(Blue,2.2f))foreach(var path in d.ReducedPaths)
            {if(path.Count>1)g.DrawLines(curve,path.Select(q=>tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P)).ToArray());foreach(StrengthPoint q in path)if(q.Nominal!=null){PointF mapped=tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P);hits.Add(new KeyValuePair<PointF,CapacityPoint>(mapped,q.Nominal));activeLayout.Curve.Add(mapped);}}
            if(d.Balanced!=null)
            {
                PointF b=tr(M(d.Balanced),d.Balanced.P);using(Pen guide=new Pen(Color.FromArgb(160,Theme.Muted),.7f)){guide.DashStyle=DashStyle.Dot;g.DrawLine(guide,chart.Left,b.Y,b.X,b.Y);g.DrawLine(guide,b.X,b.Y,b.X,tr(0,0).Y);}
                using(Pen ray=new Pen(Red,.8f)){ray.DashStyle=DashStyle.Dash;g.DrawLine(ray,tr(0,0),b);}Mark(g,b,Curve,false);hits.Add(new KeyValuePair<PointF,CapacityPoint>(b,d.Balanced));
            }
            PointF load=tr(md,d.Load.P),zero=tr(0,0);
            using(Pen guide=new Pen(Red,.85f))
            {
                guide.DashStyle=DashStyle.Dot;g.DrawLine(guide,chart.Left,load.Y,load.X,load.Y);g.DrawLine(guide,load.X,tr(md,0).Y,load.X,load.Y);guide.DashStyle=DashStyle.Solid;
                if(d.HasMoment){double k=RayEnd(d,md,d.Load.P);g.DrawLine(guide,zero,tr(md*k,d.Load.P*k));}
                if(d.AtDemand!=null&&d.HasMoment)g.DrawLine(guide,load,tr(M(d.AtDemand),d.AtDemand.P));
            }
            Mark(g,load,Red,true);if(d.AtDemand!=null){PointF c=tr(M(d.AtDemand),d.AtDemand.P);Mark(g,c,Curve,false);hits.Add(new KeyValuePair<PointF,CapacityPoint>(c,d.AtDemand));}
            if(d.ReducedAtDemand!=null){StrengthPoint q=d.ReducedAtDemand;PointF c=tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P);Mark(g,c,Blue,true);hits.Add(new KeyValuePair<PointF,CapacityPoint>(c,q.Nominal));}
            if(d.PureBending!=null){PointF m0=tr(M(d.PureBending),0);using(Pen marker=new Pen(Curve,1))g.DrawRectangle(marker,m0.X-3,m0.Y-3,6,6);}
            g.Restore(clip);
            if(d.Balanced!=null)Callout(g,"B · 평형",tr(M(d.Balanced),d.Balanced.P),chart,Curve,0);
            Callout(g,"D · 하중",load,chart,Red,0);
            if(d.AtDemand!=null)Callout(g,reduced?"Cn":"C",tr(M(d.AtDemand),d.AtDemand.P),chart,Curve,1);
            if(d.ReducedAtDemand!=null){StrengthPoint q=d.ReducedAtDemand;Callout(g,"Cr",tr(Math.Sqrt(q.Mx*q.Mx+q.My*q.My),q.P),chart,Blue,1);}
            if(d.PureBending!=null)Callout(g,"M₀ "+N(M(d.PureBending)),tr(M(d.PureBending),0),chart,Curve,1);
            if(d.ReducedAxialCap.HasValue)Callout(g,"φPn,max "+N(d.ReducedAxialCap.Value),tr(0,d.ReducedAxialCap.Value),chart,Blue,0);
            // Uniform strain need not give zero moment about the concrete centroid.
            CapacityPoint uniform=result.Curves.SelectMany(path=>path).FirstOrDefault(q=>q.Depth<0);
            if(uniform!=null)
            {
                double t=d.MomentAngle*Math.PI/180,ux=Math.Cos(t),uy=Math.Sin(t),along=uniform.Mx*ux+uniform.My*uy,cross=uniform.My*ux-uniform.Mx*uy;
                if(along>=-1e-7&&Math.Abs(cross)<Math.Max(1e-7,M(uniform)*1e-8))Callout(g,"P₀ "+N(result.PMax),tr(Math.Max(0,along),uniform.P),chart,Curve,0);
            }
            Inset(g,legend,"범례",false);
            InsetKey(g,"공칭 Pn–Mn",legend,31,Curve,DashStyle.Solid,2);
            if(reduced)InsetKey(g,"계수 φPn–φMn",legend,49,Blue,DashStyle.Solid,2.2f);
            InsetKey(g,"하중 편심 e",legend,reduced?67:49,Red,DashStyle.Solid,.85f);
            InsetKey(g,"평형 편심 eb",legend,reduced?85:67,Red,DashStyle.Dash,.8f);
            CardText(g,"ψ "+d.MomentAngle.ToString("0.00")+"°"+(d.ReducedAtDemand!=null?"   φ "+d.ReducedAtDemand.Phi.ToString("0.000"):""),legend,109,small,Theme.Muted,"legend");
            ReportRow summary=Reports.Summary(result)[result.ReportDiagrams.IndexOf(d)];string state=reduced?summary.ReducedStatus:summary.Status;
            double? capacity=reduced?summary.ReducedMn:summary.NominalMn,ratio=reduced?summary.ReducedCapacityDemandRatio:summary.NominalCapacityDemandRatio;
            Inset(g,values,"검토값",true);
            CardText(g,"Pu   "+N(d.Load.P)+" kN",values,31,small,Theme.Ink,"value");
            CardText(g,"Mu  "+N(md)+" kN·m",values,49,small,Theme.Ink,"value");
            CardText(g,(reduced?"φMn  ":"Mn    ")+(capacity.HasValue?N(capacity.Value):"—")+" kN·m",values,67,small,reduced?Blue:Curve,"value");
            CardText(g,(reduced?"φMn/Mu  ":"Mn/Mu    ")+(ratio.HasValue?ratio.Value.ToString("0.0000"):"—"),values,85,small,Theme.Ink,"value");
            string shortState=state.StartsWith("O.K.")?"O.K.":state.StartsWith("N.G.")?"N.G.":state.Contains("편람")?"편람 대조 보류":state;
            CardText(g,shortState+(reduced?" · 계수 참고":" · 공칭")+(d.ResolutionLimited?" · 세분화 한계":""),values,109,small,state.StartsWith("N.G.")?Red:Theme.Muted,"value");
            activeLayout=null;

        }
        static void Mark(Graphics g,PointF p,Color c,bool solid)
        {using(Brush b=new SolidBrush(solid?c:Color.White))using(Pen pen=new Pen(c,1.2f)){g.FillEllipse(b,p.X-3.2f,p.Y-3.2f,6.4f,6.4f);g.DrawEllipse(pen,p.X-3.2f,p.Y-3.2f,6.4f,6.4f);}}
    }
}
