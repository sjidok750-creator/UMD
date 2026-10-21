using System;
using System.Collections.Generic;
using System.Linq;

namespace PMStudio
{
    public sealed class RebarSize
    {
        public readonly string Name; public readonly double Diameter,Area;
        public RebarSize(string name,double diameter,double area){Name=name;Diameter=diameter;Area=area;}
        public override string ToString(){return Name+" · "+Area.ToString("0.##")+" mm²";}
    }
    // Published nominal areas, NOT pi times the designation number squared / 4.
    // Source: Dongkuk Steel Product Guide, reinforcing-bar nominal dimensions;
    // the user's D10-D38 reference table gives the same values (cm2 x 100 = mm2).
    public static class RebarCatalog
    {
        static readonly RebarSize[] sizes={new RebarSize("D10",9.53,71.33),new RebarSize("D13",12.7,126.7),new RebarSize("D16",15.9,198.6),new RebarSize("D19",19.1,286.5),new RebarSize("D22",22.2,387.1),new RebarSize("D25",25.4,506.7),new RebarSize("D29",28.6,642.4),new RebarSize("D32",31.8,794.2),new RebarSize("D35",34.9,956.6),new RebarSize("D38",38.1,1140.0)};
        public static IEnumerable<RebarSize> Sizes {get{return sizes;}}
        public static RebarSize Get(string name){RebarSize size=sizes.FirstOrDefault(s=>s.Name==name);if(size==null)throw new ArgumentException("지원하지 않는 철근 호칭: "+name);return size;}
    }
}
