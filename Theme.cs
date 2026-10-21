using System.Drawing;
namespace PMStudio
{
    public static class Theme
    {
        public const string FontName="맑은 고딕";
        public static readonly Color Ink=Color.FromArgb(30,43,58),Muted=Color.FromArgb(95,109,122),Accent=Color.FromArgb(38,124,69),Background=Color.FromArgb(242,244,247),Border=Color.FromArgb(210,217,223);
        public static Font Font(float size,FontStyle style) { return new Font(FontName,size,style,GraphicsUnit.Pixel); }
        public static readonly Color[] LoadColors={Color.FromArgb(32,117,178),Color.FromArgb(218,115,37),Color.FromArgb(159,70,135),Color.FromArgb(37,143,136),Color.FromArgb(105,93,170)};
    }
}
