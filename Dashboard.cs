using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PMStudio
{
    public sealed class Dashboard : UserControl
    {
        public readonly Plot Curve=new Plot{Mode="PMx",ProjectLoads=true},Section=new Plot{Compact=true},Strain=new Plot{Mode="Strain",Compact=true},Surface=new Plot{Mode="Surface"};
        readonly Label metrics=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Theme.Ink,Padding=new Padding(14,4,4,0)};
        readonly Label caption=new Label{Text="INTERACTION DIAGRAM",Dock=DockStyle.Fill,ForeColor=Theme.Ink,Font=new Font(Theme.FontName,11,FontStyle.Bold),Padding=new Padding(14,9,0,0)};
        readonly ComboBox kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=172};
        Project project;Analysis result;
        public Dashboard()
        {
            DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=Color.White;Font=new Font(Theme.FontName,9);
            Curve.Dock=DockStyle.Fill;Section.Dock=DockStyle.Fill;Strain.Dock=DockStyle.Fill;Surface.Dock=DockStyle.Fill;
            TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(5)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,180));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            TableLayoutPanel context=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};
            context.RowStyles.Add(new RowStyle(SizeType.Absolute,35));context.RowStyles.Add(new RowStyle(SizeType.Absolute,48));context.RowStyles.Add(new RowStyle(SizeType.Absolute,35));context.RowStyles.Add(new RowStyle(SizeType.Percent,100));context.Controls.Add(caption,0,0);context.Controls.Add(metrics,0,1);
            FlowLayoutPanel choose=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14,2,0,0)};choose.Controls.Add(new Label{Text="2D 보기",AutoSize=true,Padding=new Padding(0,4,5,0)});kind.Items.AddRange(new object[]{"P–Mx  (My = 0)","P–My  (Mx = 0)","Mx–My  (지정 축력)"});kind.SelectedIndex=0;kind.SelectedIndexChanged+=(s,e)=>{Curve.Mode=kind.SelectedIndex==0?"PMx":kind.SelectedIndex==1?"PMy":"Slice";Curve.Invalidate();};choose.Controls.Add(kind);context.Controls.Add(choose,0,2);
            FlowLayoutPanel options=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12,0,0,0),WrapContents=true};
            Option(options,"공칭내력",v=>{Curve.ShowNominal=v;Surface.ShowNominal=v;});Option(options,"면 채움",v=>Surface.ShowSurface=v);Option(options,"하중",v=>{Curve.ShowLoads=v;Surface.ShowLoads=v;});Option(options,"절단선",v=>{Curve.ShowSlice=v;Surface.ShowSlice=v;});Option(options,"격자",v=>{Curve.ShowGrid=v;Surface.ShowGrid=v;});
            Option(options,"균형점",v=>Curve.ShowReferences=v);
            Button reset=new Button{Text="시점 초기화",AutoSize=true,Height=25,FlatStyle=FlatStyle.System};reset.Click+=(s,e)=>Surface.ResetCamera();options.Controls.Add(reset);context.Controls.Add(options,0,3);
            TableLayoutPanel state=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));state.Controls.Add(Section,0,0);state.Controls.Add(Strain,1,0);
            layout.Controls.Add(context,0,0);layout.Controls.Add(state,1,0);layout.Controls.Add(Curve,0,1);layout.Controls.Add(Surface,1,1);Controls.Add(layout);
        }
        void Option(FlowLayoutPanel host,string text,Action<bool> changed)
        {
            CheckBox box=new CheckBox{Text=text,Checked=true,AutoSize=true,Margin=new Padding(3,4,9,3)};box.CheckedChanged+=(s,e)=>{changed(box.Checked);Curve.Invalidate();Surface.Invalidate();};host.Controls.Add(box);
        }
        public void Bind(Project p,Analysis r)
        {
            project=p;result=r;foreach(Plot plot in new[]{Curve,Section,Strain,Surface}){plot.Project=p;plot.Result=r;plot.Selected=null;plot.Invalidate();}
            if(r!=null&&r.Limits!=null&&r.Limits.BalancedMx.Count>0){CapacityPoint point=r.Limits.BalancedMx[r.Limits.BalancedMx.Count-1];Section.Selected=point;Strain.Selected=point;}
            metrics.Text=r==null?"RC 공칭 단면내력 · 상관면 계산(F5) 후 결과 표시":string.Format("P₀ {0:#,0.0} kN   ·   Pmin {1:#,0.0} kN",r.PMax,r.PMin);
            if(r!=null&&r.Limits.BalancedMx.Count>0){CapacityPoint b=r.Limits.BalancedMx[r.Limits.BalancedMx.Count-1];metrics.Text+=string.Format("\r\nB+: Pb {0:#,0.0} kN  /  Mb {1:#,0.0} kN·m",b.P,b.Mx);}
        }
        public void SelectPoint(CapacityPoint point) { foreach(Plot p in new[]{Curve,Section,Strain,Surface}){p.Selected=point;p.Invalidate();} }
        public void SaveImage(string path,int width,int height)
        {
            if(project==null||result==null)throw new InvalidOperationException("계산 결과가 없습니다.");
            if(width<1200||height<850)throw new ArgumentException("그림 크기는 1200×850 이상이어야 합니다.");
            using(Bitmap image=new Bitmap(width,height))using(Graphics g=Graphics.FromImage(image))
            {
                float scale=width/1200f;float logicalHeight=height/scale;if(logicalHeight<750)throw new ArgumentException("그림 높이가 너무 작습니다.");image.SetResolution(96*scale,96*scale);g.Clear(Color.White);g.ScaleTransform(scale,scale);g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
                using(Font title=Theme.Font(24,FontStyle.Bold))using(Font normal=Theme.Font(14,FontStyle.Regular))using(Brush ink=new SolidBrush(Theme.Ink))
                {
                    g.DrawString("PM Studio +  |  P–M / Biaxial Interaction",title,ink,32,20);g.DrawString(project.Name+"  ·  RC 공칭 단면내력  ·  mm / MPa / kN / kN·m",normal,ink,35,59);
                    g.DrawString(string.Format("P₀ = {0:#,0.000} kN\r\nPmin = {1:#,0.000} kN\r\nAg = {2:#,0} mm²   /   As = {3:#,0.00} mm²",result.PMax,result.PMin,result.GrossArea,result.SteelArea),normal,ink,45,112);
                    g.DrawString("실선 공칭내력  /  빈 기호 양축하중 투영\r\n보라색 선: 지정 축력의 절단면\r\n공칭 모델 · 설계기준의 계수 및 축력상한 미적용",normal,ink,45,197);
                    g.DrawString("RC-RectangularBlock-Nominal-v1  |  반대축 모멘트와 단위·부호를 확인하세요.",normal,ink,35,logicalHeight-33);
                }
                g.ResetTransform();int half=576,top=285,bottom=(int)logicalHeight-top-52;Composite(g,Section,(int)((half+28)*scale),(int)(70*scale),(int)(310*scale),(int)(205*scale),scale);Composite(g,Strain,(int)((half+338)*scale),(int)(70*scale),(int)(240*scale),(int)(205*scale),scale);Composite(g,Curve,(int)(16*scale),(int)(top*scale),(int)(half*scale),(int)(bottom*scale),scale);Composite(g,Surface,(int)((half+30)*scale),(int)(top*scale),(int)(half*scale),(int)(bottom*scale),scale);image.Save(path,ImageFormat.Png);
            }
            Curve.Invalidate();Section.Invalidate();Strain.Invalidate();Surface.Invalidate();
        }
        static void Composite(Graphics destination,Plot plot,int x,int y,int width,int height,float scale)
        {
            using(Bitmap image=new Bitmap(width,height)){image.SetResolution(96*scale,96*scale);using(Graphics g=Graphics.FromImage(image))plot.Render(g,width,height);destination.DrawImage(image,new Rectangle(x,y,width,height),0,0,width,height,GraphicsUnit.Pixel);}
        }
    }
}
