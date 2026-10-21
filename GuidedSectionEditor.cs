using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace PMStudio
{
    public sealed class GuidedSectionEditor : IDisposable
    {
        public readonly SectionInputPage ShapePage,RebarPage;
        public SectionRecipe Draft {get;private set;}
        public SectionRecipe AppliedRecipe {get;private set;}
        public bool Pending {get;private set;}
        public Project Preview {get;private set;}
        public string Error {get;private set;}
        public Action ApplyRequested;
        public Action DraftChanged;
        public Action<bool> AdvancedRequested;
        public float LayoutScale=1;
        bool loading;Project original;
        public GuidedSectionEditor(){ShapePage=new SectionInputPage(this,false);RebarPage=new SectionInputPage(this,true);}
        public void LoadProject(Project p)
        {
            loading=true;try{original=p;Draft=SectionFactory.Infer(p);AppliedRecipe=Draft==null?null:Draft.Copy();Pending=false;Preview=p;Error="";ShapePage.Rebuild();RebarPage.Rebuild();}finally{loading=false;}RefreshPreview();
        }
        public void Choose(string shape)
        {
            if(loading)return;
            if(shape=="Custom"){Draft=null;Preview=original;Pending=false;Error="";}
            else
            {
                Draft=Draft==null?new SectionRecipe():Draft.Copy();Draft.Shape=shape;
                if(shape=="Circle"||shape=="HollowCircle"){Draft.Width=1000;Draft.Height=1000;Draft.InnerWidth=560;Draft.Bar="D29";Draft.ClearCover=56.17;Draft.Count=16;Draft.SecondRing=false;}
                if(shape=="Box"){Draft.Width=1200;Draft.Height=900;Draft.InnerWidth=700;Draft.InnerHeight=450;Draft.ClearCover=50;Draft.Nx=5;Draft.Ny=4;Draft.SecondRing=false;}
                if(shape=="Rectangle"){Draft.Width=600;Draft.Height=800;Draft.Bar="D25";Draft.ClearCover=37.77;Draft.Nx=4;Draft.Ny=4;Draft.SecondRing=false;}
                if(shape=="Stadium"){Draft.Width=2400;Draft.Height=1000;Draft.ClearCover=50;Draft.Count=32;Draft.SecondRing=false;}
                Pending=true;
            }
            Rebuild();RefreshPreview();if(DraftChanged!=null)DraftChanged();
        }
        public void Change(Action<SectionRecipe> change,bool rebuild)
        {if(loading||Draft==null)return;change(Draft);Pending=true;if(rebuild)Rebuild();RefreshPreview();if(DraftChanged!=null)DraftChanged();}
        void Rebuild(){loading=true;try{ShapePage.Rebuild();RebarPage.Rebuild();}finally{loading=false;}}
        void RefreshPreview()
        {
            Error="";
            if(Draft!=null)
            {
                Preview=null;try{Preview=SectionFactory.Build(Draft,false);Engine.Validate(Preview);}catch(ArgumentException ex){Error=ex.Message;}
            }
            ShapePage.RefreshPicture();RebarPage.RefreshPicture();
        }
        public Project Build(){if(Draft==null)throw new ArgumentException("형상 목록에서 새 형상을 선택하세요. 현재 사용자 형상은 그대로 유지됩니다.");return SectionFactory.Build(Draft,true);}
        public void CommitEdits(){ShapePage.ValidateChildren();RebarPage.ValidateChildren();}
        public void Accept(Project p){original=p;AppliedRecipe=Draft==null?null:Draft.Copy();Pending=false;RefreshPreview();}
        public void RequestApply(){if(ApplyRequested!=null)ApplyRequested();}
        public void RequestAdvanced(bool rebar){if(AdvancedRequested!=null)AdvancedRequested(rebar);}
        public void SetScale(float scale){LayoutScale=scale;Rebuild();RefreshPreview();}
        public void Dispose(){ShapePage.Dispose();RebarPage.Dispose();}
    }
    public sealed class SectionInputPage : UserControl
    {
        readonly GuidedSectionEditor editor;readonly bool rebar;
        readonly Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        readonly FlowLayoutPanel content=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10,6,10,8)};
        readonly Label hint=new Label{Height=42,ForeColor=Theme.Muted},state=new Label{Height=54,ForeColor=Theme.Muted};
        readonly TableLayoutPanel fields=new TableLayoutPanel{AutoSize=false,ColumnCount=2,Margin=new Padding(0,4,0,4)};
        readonly ComboBox shape=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Height=28};
        readonly Label name=new Label{Height=27,ForeColor=Theme.Ink,TextAlign=ContentAlignment.MiddleLeft};
        public readonly SectionSketch Sketch=new SectionSketch();
        public readonly Button ApplyButton=new Button{Text="형상·배근 적용",Height=34,BackColor=Color.FromArgb(224,238,229),FlatStyle=FlatStyle.Flat};
        readonly LinkLabel advanced=new LinkLabel{Text="고급: 좌표로 편집…",Height=26,LinkColor=Theme.Muted};
        bool updating;
        public SectionInputPage(GuidedSectionEditor owner,bool reinforcement)
        {
            editor=owner;rebar=reinforcement;Font=new Font(Theme.FontName,9.5f);BackColor=Theme.Background;AutoScaleMode=AutoScaleMode.None;Dock=DockStyle.Fill;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
            hint.Text=rebar?"피복·규격·개수를 넣으면\r\n철근 위치와 철근량을 자동 산정합니다.":"형상을 고르고 그림에 표시된\r\n치수를 입력하세요. 단위: mm";
            shape.Items.AddRange(SectionFactory.ShapeNames.Cast<object>().Concat(new object[]{"사용자 형상 (현재 유지)"}).ToArray());
            shape.SelectedIndexChanged+=(s,e)=>{if(!updating&&shape.SelectedIndex>=0)editor.Choose(shape.SelectedIndex<5?SectionFactory.Shapes[shape.SelectedIndex]:"Custom");};
            Sketch.ShowRebar=rebar;Sketch.Height=rebar?190:210;if(rebar)state.Height=74;
            content.Controls.Add(hint);content.Controls.Add(rebar?(Control)name:shape);content.Controls.Add(Sketch);content.Controls.Add(fields);content.Controls.Add(state);content.Controls.Add(ApplyButton);content.Controls.Add(advanced);scroll.Controls.Add(content);Controls.Add(scroll);
            ApplyButton.Click+=(s,e)=>editor.RequestApply();advanced.LinkClicked+=(s,e)=>editor.RequestAdvanced(rebar);
            scroll.Resize+=(s,e)=>ResizeContent();ResizeContent();
        }
        void ResizeContent()
        {
            float scale=editor.LayoutScale;int width=Math.Max((int)(220*scale),scroll.ClientSize.Width-(int)(22*scale)-SystemInformation.VerticalScrollBarWidth);
            foreach(Control c in new Control[]{hint,shape,name,Sketch,fields,state,ApplyButton,advanced})c.Width=width;
        }
        void Row(string label,Control input,string parameter)
        {
            int n=fields.RowCount++;fields.RowStyles.Add(new RowStyle(SizeType.Absolute,34*editor.LayoutScale));
            var text=new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};input.Dock=DockStyle.Fill;input.Margin=new Padding(1,(int)(3*editor.LayoutScale),1,(int)(3*editor.LayoutScale));fields.Controls.Add(text,0,n);fields.Controls.Add(input,1,n);
            input.Enter+=(s,e)=>{Sketch.ActiveParameter=parameter;Sketch.Invalidate();};input.Leave+=(s,e)=>{Sketch.ActiveParameter=null;Sketch.Invalidate();};
        }
        NumericUpDown Number(string label,double value,double min,double max,int places,string parameter,Action<SectionRecipe,decimal> set)
        {
            var input=new NumericUpDown{Minimum=(decimal)min,Maximum=(decimal)max,DecimalPlaces=places,ThousandsSeparator=true,TextAlign=HorizontalAlignment.Right,Value=(decimal)Math.Max(min,Math.Min(max,value)),Increment=places==0?1:5};
            input.Name=parameter;input.ValueChanged+=(s,e)=>{if(!updating)editor.Change(r=>set(r,input.Value),false);};Row(label,input,parameter);return input;
        }
        void BarChoice(string label,string selected,bool tie)
        {
            var choice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};choice.Items.AddRange(RebarCatalog.Sizes.Select(s=>(object)s.Name).ToArray());if(!tie)choice.Items.Add("직접 입력");choice.SelectedItem=selected=="Custom"?"직접 입력":selected;
            choice.Name=tie?"Tie":"Bar";choice.SelectedIndexChanged+=(s,e)=>{if(!updating)editor.Change(r=>{if(tie)r.Tie=(string)choice.SelectedItem;else{r.Bar=choice.SelectedIndex==10?"Custom":(string)choice.SelectedItem;if(r.Bar!="Custom"){var size=RebarCatalog.Get(r.Bar);r.Area=size.Area;r.Diameter=size.Diameter;}}},!tie);};Row(label,choice,tie?"c":"As");
        }
        public void Rebuild()
        {
            updating=true;fields.SuspendLayout();try
            {
                foreach(Control c in fields.Controls.Cast<Control>().ToArray())c.Dispose();fields.Controls.Clear();fields.RowStyles.Clear();fields.RowCount=0;SectionRecipe r=editor.Draft;
                shape.SelectedIndex=r==null?5:Array.IndexOf(SectionFactory.Shapes,r.Shape);name.Text=r==null?"사용자 형상 · 기존 배근 유지":SectionFactory.Name(r.Shape)+" · 배근 설정";
                if(r==null)
                {var note=new Label{Text="기존 형상·철근을 그대로 유지합니다.\r\n형상 탭에서 표준 형상을 선택하거나\r\n아래 고급 편집을 사용할 수 있습니다.",Height=72,AutoSize=false,Dock=DockStyle.Fill};fields.Controls.Add(note,0,0);fields.SetColumnSpan(note,2);fields.RowCount=1;fields.RowStyles.Add(new RowStyle(SizeType.Absolute,78*editor.LayoutScale));}
                else if(!rebar)
                {
                    bool circle=r.Shape=="Circle"||r.Shape=="HollowCircle";
                    Number(circle?"외경 D (mm)":"폭 B (mm)",r.Width,10,50000,2,"B",(q,v)=>q.Width=(double)v);
                    if(!circle)Number("높이 H (mm)",r.Height,10,50000,2,"H",(q,v)=>q.Height=(double)v);
                    if(r.Shape=="HollowCircle")Number("내경 Di (mm)",r.InnerWidth,1,49999,2,"Di",(q,v)=>q.InnerWidth=(double)v);
                    if(r.Shape=="Box"){Number("내부 폭 Bi (mm)",r.InnerWidth,1,49999,2,"Bi",(q,v)=>q.InnerWidth=(double)v);Number("내부 높이 Hi (mm)",r.InnerHeight,1,49999,2,"Hi",(q,v)=>q.InnerHeight=(double)v);}
                }
                else
                {
                    BarChoice("주근 규격",r.Bar,false);BarChoice("띠철근 규격",r.Tie,true);
                    if(r.Bar=="Custom"){Number("주근 As (mm²)",r.Area,.01,20000,4,"As",(q,v)=>q.Area=(double)v);Number("주근 직경 (mm)",r.Diameter,.1,160,4,"db",(q,v)=>q.Diameter=(double)v);}
                    Number("순피복 c (mm)",r.ClearCover,0,5000,2,"c",(q,v)=>q.ClearCover=(double)v);
                    bool rectangle=r.Shape=="Rectangle"||r.Shape=="Box",circle=r.Shape=="Circle"||r.Shape=="HollowCircle";
                    if(rectangle){Number("상·하면 Nx (개)",r.Nx,2,100,0,"Nx",(q,v)=>q.Nx=(int)v);Number("좌·우면 Ny (개)",r.Ny,2,100,0,"Ny",(q,v)=>q.Ny=(int)v);}
                    else Number(circle?"외측 링 N (개)":"둘레 총 N (개)",r.Count,circle?3:4,300,0,"N",(q,v)=>q.Count=(int)v);
                    if(circle)
                    {
                        var two=new CheckBox{Text="동심 2열 배근",Checked=r.SecondRing,AutoSize=true};two.CheckedChanged+=(s,e)=>{if(!updating)editor.Change(q=>q.SecondRing=two.Checked,true);};Row("배근 열",two,"s");
                        if(r.SecondRing){Number("내측 링 N₂ (개)",r.InnerCount,3,300,0,"N2",(q,v)=>q.InnerCount=(int)v);Number("링 간격 s (mm)",r.RingSpacing,1,10000,2,"s",(q,v)=>q.RingSpacing=(double)v);}
                        Number("시작 각도 θ (°)",r.StartAngle,-360,360,2,"theta",(q,v)=>q.StartAngle=(double)v);
                    }
                }
            }finally{fields.Height=(int)Math.Ceiling(fields.RowStyles.Cast<RowStyle>().Sum(r=>r.Height));fields.ResumeLayout(true);updating=false;}ResizeContent();
        }
        public void RefreshPicture()
        {
            Sketch.Project=editor.Preview;Sketch.Recipe=editor.Draft;Sketch.Invalid=!string.IsNullOrEmpty(editor.Error);Sketch.Invalidate();ApplyButton.Enabled=editor.Draft!=null;
            if(!string.IsNullOrEmpty(editor.Error)){state.ForeColor=Color.Firebrick;state.Text=editor.Error;}
            else if(editor.Preview!=null)
            {
                state.ForeColor=editor.Pending?Theme.Accent:Theme.Muted;
                state.Text=(editor.Pending?"변경 미리보기 · 적용 또는 F5\r\n":"")+"주근 "+editor.Preview.Bars.Count+"개 · As "+editor.Preview.Bars.Sum(b=>b.Area).ToString("N2")+" mm²"+(rebar?"\r\nc: 콘크리트 표면 → 띠철근 바깥면":"")+(rebar&&editor.Draft!=null&&(editor.Draft.Shape=="Rectangle"||editor.Draft.Shape=="Box")?"\r\n모서리 철근은 중복 없이 합산":"");
            }
        }
    }
    public sealed class SectionSketch : Control
    {
        public Project Project;public SectionRecipe Recipe;public bool ShowRebar,Invalid;public string ActiveParameter;
        public SectionSketch(){DoubleBuffered=true;BackColor=Color.White;SetStyle(ControlStyles.ResizeRedraw,true);}
        public void Render(Graphics g,int width,int height)
        {
            var saved=g.Save();float dpi=g.DpiX/96;g.ScaleTransform(dpi,dpi);float w=width/dpi,h=height/dpi;g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            using(var edge=new Pen(Invalid?Color.IndianRed:Theme.Border,1))g.DrawRectangle(edge,.5f,.5f,w-1,h-1);
            if(Project==null||Project.Contours.Count==0){g.Restore(saved);return;}
            var points=Project.Contours.SelectMany(c=>c.Points).ToArray();double minx=points.Min(p=>p.X),maxx=points.Max(p=>p.X),miny=points.Min(p=>p.Y),maxy=points.Max(p=>p.Y);
            double scale=Math.Min((w-82)/Math.Max(1,maxx-minx),(h-76)/Math.Max(1,maxy-miny));float cx=w/2,cy=(h-12)/2;
            Func<double,double,PointF> tr=(x,y)=>new PointF(cx+(float)((x-(minx+maxx)/2)*scale),cy-(float)((y-(miny+maxy)/2)*scale));
            using(var path=new GraphicsPath(FillMode.Alternate))using(var fill=new SolidBrush(Color.FromArgb(248,224,191)))using(var border=new Pen(Theme.Ink,1.2f))
            {foreach(Contour c in Project.Contours)path.AddPolygon(c.Points.Select(p=>tr(p.X,p.Y)).ToArray());g.FillPath(fill,path);g.DrawPath(border,path);}
            if(ShowRebar&&Recipe!=null)
            {
                double cover=Recipe.ClearCover+RebarCatalog.Get(Recipe.Tie).Diameter/2;PointF a=tr(minx+cover,maxy-cover),b=tr(maxx-cover,miny+cover);
                if(b.X>a.X&&b.Y>a.Y)using(var tie=new Pen(Color.FromArgb(118,135,143),1)){tie.DashStyle=DashStyle.Dash;if(Recipe.Shape=="Circle"||Recipe.Shape=="HollowCircle")g.DrawEllipse(tie,a.X,a.Y,b.X-a.X,b.Y-a.Y);else if(Recipe.Shape=="Rectangle"||Recipe.Shape=="Box")g.DrawRectangle(tie,a.X,a.Y,b.X-a.X,b.Y-a.Y);}
            }
            foreach(Rebar bar in Project.Bars){PointF p=tr(bar.X,bar.Y);float d=(float)Math.Max(ShowRebar?4:2.5,2*Math.Sqrt(bar.Area/Math.PI)*scale);g.FillEllipse(ShowRebar?Brushes.Black:Brushes.DimGray,p.X-d/2,p.Y-d/2,d,d);}
            PointF tl=tr(minx,maxy),br=tr(maxx,miny);bool circle=Recipe!=null&&(Recipe.Shape=="Circle"||Recipe.Shape=="HollowCircle");
            if(!ShowRebar)
            {
                Dimension(g,new PointF(tl.X,br.Y+17),new PointF(br.X,br.Y+17),(circle?"D":"B")+" = "+(maxx-minx).ToString("0.##"),ActiveParameter=="B",false);
                if(!circle)Dimension(g,new PointF(tl.X-18,tl.Y),new PointF(tl.X-18,br.Y),"H = "+(maxy-miny).ToString("0.##"),ActiveParameter=="H",true);
                if(Recipe!=null&&(Recipe.Shape=="HollowCircle"||Recipe.Shape=="Box"))
                {float left=tr(-Recipe.InnerWidth/2,0).X,right=tr(Recipe.InnerWidth/2,0).X;Dimension(g,new PointF(left,cy),new PointF(right,cy),(circle?"Di":"Bi")+" = "+Recipe.InnerWidth.ToString("0.##"),ActiveParameter==(circle?"Di":"Bi"),false);if(Recipe.Shape=="Box")Label(g,"Hi = "+Recipe.InnerHeight.ToString("0.##"),cx,cy+20,ActiveParameter=="Hi");}
            }
            else if(Recipe!=null)
            {
                bool rectangle=Recipe.Shape=="Rectangle"||Recipe.Shape=="Box";
                Label(g,rectangle?"Nx = "+Recipe.Nx:"N = "+Recipe.Count,cx,12,ActiveParameter==(rectangle?"Nx":"N"));
                Label(g,rectangle?"Ny = "+Recipe.Ny:Recipe.SecondRing?"N₂ = "+Recipe.InnerCount:"",cx,h-29,ActiveParameter==(rectangle?"Ny":"N2"));
                using(var pen=new Pen(Theme.Accent,.8f))g.DrawLine(pen,new PointF(br.X+2,tl.Y+4),new PointF(Math.Min(w-8,br.X+16),Math.Max(30,tl.Y-12)));
                Label(g,"c = "+Recipe.ClearCover.ToString("0.##"),Math.Min(w-43,br.X+3),Math.Max(28,tl.Y-25),ActiveParameter=="c");
            }
            g.Restore(saved);
        }
        static void Label(Graphics g,string text,float cx,float y,bool active)
        {if(text.Length==0)return;using(var font=Theme.Font(12,active?FontStyle.Bold:FontStyle.Regular))using(var brush=new SolidBrush(active?Theme.Accent:Theme.Ink)){var size=g.MeasureString(text,font);g.FillRectangle(Brushes.White,cx-size.Width/2-2,y,size.Width+4,size.Height);g.DrawString(text,font,brush,cx-size.Width/2,y);}}
        static void Dimension(Graphics g,PointF a,PointF b,string text,bool active,bool vertical)
        {
            using(var pen=new Pen(active?Theme.Accent:Color.FromArgb(122,139,153),.8f)){g.DrawLine(pen,a,b);g.DrawLine(pen,a.X-3,a.Y+3,a.X+3,a.Y-3);g.DrawLine(pen,b.X-3,b.Y+3,b.X+3,b.Y-3);}
            if(vertical){var save=g.Save();g.TranslateTransform(a.X-17,(a.Y+b.Y)/2);g.RotateTransform(-90);Label(g,text,0,0,active);g.Restore(save);}else Label(g,text,(a.X+b.X)/2,a.Y-8,active);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Render(e.Graphics,Width,Height);}
    }
}
