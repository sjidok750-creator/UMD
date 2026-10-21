using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Collections.Generic;

namespace PMStudio
{
    public static class Reports
    {
        public static List<ReportRow> Summary(Analysis r)
        {
            List<ReportRow> rows=new List<ReportRow>();string[] names={"Pmax","M(X-dir)max","M(Y-dir)max"};
            for(int i=0;i<r.ReportDiagrams.Count;i++)
            {
                DirectionalDiagram d=r.ReportDiagrams[i];ReportRow row=new ReportRow{Division=names[i],Combination=d.Load.Name,P=d.Load.P,Mx=d.Load.Mx,My=d.Load.My,Mu=Math.Sqrt(d.Load.Mx*d.Load.Mx+d.Load.My*d.Load.My),Status="미확정",Note="Mn 공칭내력. φ 및 축력상한 미적용. 동일 P, 합성 모멘트 방향 고정."};
                if(d.AtDemand!=null)row.NominalMn=Math.Sqrt(d.AtDemand.Mx*d.AtDemand.Mx+d.AtDemand.My*d.AtDemand.My);
                if(row.Mu>1e-9&&row.NominalMn.HasValue)row.NominalCapacityDemandRatio=row.NominalMn.Value/row.Mu;
                if(!d.HasMoment){row.Status="—";row.Note+=" 영모멘트로 내력/요구 비율 미정의.";}
                else if(d.Check!=null&&d.Check.Status=="OutsideNominal"&&(row.P<r.PMin||row.P>r.PMax)){row.Status="N.G. (공칭)";row.Note+=" 공칭 축력 범위 밖.";}
                else if(d.Check!=null&&d.Check.Status!="Unresolved"&&d.Check.Utilization.HasValue&&row.NominalCapacityDemandRatio.HasValue&&!d.ResolutionLimited)
                {
                    double direct=row.NominalMn.Value>1e-12?row.Mu/row.NominalMn.Value:double.PositiveInfinity;
                    bool inside=direct<=1+1e-8;
                    if(Math.Abs(direct-d.Check.Utilization.Value)<=Math.Max(1e-8,direct*0.01)&&inside==(d.Check.Status=="InsideNominal"))row.Status=inside?"O.K. (공칭)":"N.G. (공칭)";
                    else row.Note+=" 직접 평형과 각도 분할 검토의 차이 또는 경계 판정 불일치.";
                }
                else row.Note+=" 사용률 미정의 또는 수렴 미확정. 상세 검토 필요.";
                if(d.ReductionProfile!="Nominal")
                {
                    row.ReducedStatus="미확정";row.Note+=" "+Reduction.Label(d.ReductionProfile)+". 부재 전체 기준 적합 판정 아님.";
                    if(d.ReducedAtDemand!=null)row.ReducedMn=Math.Sqrt(d.ReducedAtDemand.Mx*d.ReducedAtDemand.Mx+d.ReducedAtDemand.My*d.ReducedAtDemand.My);
                    if(row.Mu>1e-9&&row.ReducedMn.HasValue)row.ReducedCapacityDemandRatio=row.ReducedMn.Value/row.Mu;
                    if(!d.HasMoment)row.ReducedStatus="—";
                    else if(d.ReducedAxialCap.HasValue&&(row.P>d.ReducedAxialCap.Value+1e-7||row.P<(d.ReductionProfile=="Reference-KR2008-Tied"?0.85:0.9)*r.PMin-1e-7))row.ReducedStatus="N.G. (참고)";
                    else if(d.ReducedCheck!=null&&d.ReducedCheck.Status!="Unresolved"&&d.ReducedCheck.Utilization.HasValue&&row.ReducedMn.HasValue&&!d.ResolutionLimited&&d.ReducedIntersectionCount==1)
                    {
                        double direct=row.ReducedMn.Value>1e-12?row.Mu/row.ReducedMn.Value:double.PositiveInfinity;bool inside=direct<=1+1e-8;
                        if(Math.Abs(direct-d.ReducedCheck.Utilization.Value)<=Math.Max(1e-8,direct*0.01)&&inside==(d.ReducedCheck.Status=="InsideNominal"))row.ReducedStatus=inside?"O.K. (참고)":"N.G. (참고)";
                    }
                    if(d.ReducedIntersectionCount>1)row.Note+=" 계수곡선에 복수 교점. 최소 모멘트 교점을 참고 표시하며 자동 O.K. 판정하지 않음.";
                    if(d.ReductionProfile=="Reference-KR2008-Tied"){row.ReducedStatus="편람 대조 미확정";row.Note+=" 편람의 평형편심 545.084 mm와 최외단 철근 기준 계산에 차이가 있어 원 예제 재현 판정을 보류함. 현재 곡선의 내력값만 참고 표시.";}
                }
                rows.Add(row);
            }
            return rows;
        }
        public static void SummaryCsv(string path,Analysis r)
        {
            StringBuilder s=new StringBuilder("Division,Combination,P_kN,Mx_kNm,My_kNm,Demand_Mu_kNm,Nominal_Mn_kNm,Nominal_Mn_over_Mu,NominalRemark,Reference_phiMn_kNm,Reference_phiMn_over_Mu,ReferenceRemark,Note\r\n");
            foreach(ReportRow row in Summary(r))s.AppendLine(C(row.Division)+","+C(row.Combination)+","+Files.N(row.P)+","+Files.N(row.Mx)+","+Files.N(row.My)+","+Files.N(row.Mu)+","+(row.NominalMn.HasValue?Files.N(row.NominalMn.Value):"")+","+(row.NominalCapacityDemandRatio.HasValue?Files.N(row.NominalCapacityDemandRatio.Value):"")+","+C(row.Status)+","+(row.ReducedMn.HasValue?Files.N(row.ReducedMn.Value):"")+","+(row.ReducedCapacityDemandRatio.HasValue?Files.N(row.ReducedCapacityDemandRatio.Value):"")+","+C(row.ReducedStatus)+","+C(row.Note));
            File.WriteAllText(path,s.ToString(),new UTF8Encoding(true));
        }
        public static string Hash(Project p)
        {
            using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Files.Json(p)))).Replace("-","").ToLowerInvariant();
        }
        static string H(object s) { return HttpUtility.HtmlEncode(Convert.ToString(s,CultureInfo.InvariantCulture)); }
        static string C(string s)
        {
            string trimmed=s.TrimStart();if(trimmed.Length>0&&"=+-@".IndexOf(trimmed[0])>=0)s="'"+s;
            return "\""+s.Replace("\"","\"\"")+"\"";
        }
        public static void Csv(string path,Project p,Analysis r)
        {
            StringBuilder s=new StringBuilder();s.AppendLine("Record,Name,Angle_deg,c_mm,P_kN,Mx_kNm,My_kNm,Status,FixedP_MomentUtilization");
            s.AppendLine("Metadata,"+C("Nominal RC block; no design code; input-sha256="+Hash(p))+",,,,,,,");
            foreach(var curve in r.Curves)foreach(CapacityPoint q in curve)s.AppendLine("Surface,,"+Files.N(q.Angle)+","+(q.Depth<0?"Infinity":Files.N(q.Depth))+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+",,");
            foreach(CapacityPoint q in r.Slice)s.AppendLine("Slice,,"+Files.N(q.Angle)+","+(q.Depth<0?"Infinity":Files.N(q.Depth))+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+",,");
            foreach(var paths in new[]{r.PMxPaths,r.PMyPaths})foreach(var line in paths)foreach(CapacityPoint q in line)s.AppendLine((paths==r.PMxPaths?"PMx":"PMy")+",,"+Files.N(q.Angle)+","+(q.Depth<0?"Infinity":Files.N(q.Depth))+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+",,");
            var referenceSets=new[]{r.Limits.BalancedMx,r.Limits.BalancedMy,r.Limits.PureBendingMx,r.Limits.PureBendingMy};string[] referenceNames={"BalancedMx","BalancedMy","PureBendingMx","PureBendingMy"};
            for(int i=0;i<referenceSets.Length;i++)foreach(CapacityPoint q in referenceSets[i])s.AppendLine("Reference,"+referenceNames[i]+","+Files.N(q.Angle)+","+Files.N(q.Depth)+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+",,");
            foreach(DemandResult d in r.Demands)s.AppendLine("Demand,"+C(d.Name)+",,,"+Files.N(d.P)+","+Files.N(d.Mx)+","+Files.N(d.My)+","+d.Status+","+(d.Utilization.HasValue?Files.N(d.Utilization.Value):""));
            foreach(DirectionalDiagram diagram in r.ReportDiagrams)for(int pathIndex=0;pathIndex<diagram.Paths.Count;pathIndex++)foreach(CapacityPoint q in diagram.Paths[pathIndex])s.AppendLine("ReportCurve,"+C(diagram.Title+"; "+diagram.Load.Name+"; psi="+Files.N(diagram.MomentAngle)+"; path="+pathIndex)+","+Files.N(q.Angle)+","+(q.Depth<0?"Infinity":Files.N(q.Depth))+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+",,");
            foreach(DirectionalDiagram diagram in r.ReportDiagrams)for(int pathIndex=0;pathIndex<diagram.ReducedPaths.Count;pathIndex++)foreach(StrengthPoint q in diagram.ReducedPaths[pathIndex])s.AppendLine("ReportReduced,"+C(diagram.Title+"; "+diagram.Load.Name+"; path="+pathIndex)+","+(q.Nominal==null?"":Files.N(q.Nominal.Angle))+","+(q.Nominal==null?"Cap":q.Nominal.Depth<0?"Infinity":Files.N(q.Nominal.Depth))+","+Files.N(q.P)+","+Files.N(q.Mx)+","+Files.N(q.My)+","+C("Reference phi="+Files.N(q.Phi))+",");
            File.WriteAllText(path,s.ToString(),new UTF8Encoding(true));
        }
        public static void Html(string path,Project p,Analysis r)
        {
            StringBuilder s=new StringBuilder("<!doctype html><html lang='ko'><meta charset='utf-8'><title>PM Studio +</title><style>body{font:14px 'Malgun Gothic','맑은 고딕',sans-serif;margin:35px;color:#222;background:#fff}h1{color:#267c45;font-style:normal}table{border-collapse:collapse;width:100%;background:white;margin:20px 0}td,th{border:1px solid #aaa;padding:8px;text-align:right}th{background:#edf2ef}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:white;padding:15px}small{overflow-wrap:anywhere}</style><h1>PM Studio +</h1>");
            s.Append("<h2>"+H(p.Name)+" — 공칭 단면내력</h2><p>"+H(r.Engine)+" / "+H(r.Units)+"</p><p>"+H(r.Strength)+"</p>");
            s.Append("<h2>교각 P–M 상관도 검토 요약</h2><p>Mu = √(Mx²+My²). 각 최대값이 발생한 조합의 P·Mx·My 동시 발생값. 동일 P에서의 방향별 공칭내력 Mn으로 비교합니다. Mn/Mu ≥ 1은 공칭영역 검토이며, φMn/Mu 설계판정이 아닙니다.</p><table><tr><th>구분</th><th>하중조합</th><th>P (kN)</th><th>Demand Mu (kN·m)</th><th>Capacity Mn (공칭, kN·m)</th><th>Mn/Mu</th><th>Remark</th></tr>");
            foreach(ReportRow row in Summary(r))s.Append("<tr><td>"+H(row.Division)+"</td><td>"+H(row.Combination)+"</td><td>"+H(row.P.ToString("N2",CultureInfo.InvariantCulture))+"</td><td>"+H(row.Mu.ToString("N2",CultureInfo.InvariantCulture))+"</td><td>"+(row.NominalMn.HasValue?H(row.NominalMn.Value.ToString("N2",CultureInfo.InvariantCulture)):"—")+"</td><td>"+(row.NominalCapacityDemandRatio.HasValue?H(row.NominalCapacityDemandRatio.Value.ToString("0.0000",CultureInfo.InvariantCulture)):"—")+"</td><td>"+H(row.Status)+"</td></tr>");
            s.Append("</table>");
            if(p.ReductionProfile!="Nominal")
            {
                s.Append("<h2>계수 적용 참고 비교</h2><p>"+H(Reduction.Label(p.ReductionProfile))+". 현재 KDS 및 부재 전체 설계판정이 아닙니다. 동일 검토 축력에서 φPn=Pu를 만족하는 내력점을 다시 계산합니다.</p><table><tr><th>구분</th><th>Demand Mu (kN·m)</th><th>Capacity φMn (참고, kN·m)</th><th>φMn/Mu</th><th>Remark</th></tr>");
                foreach(ReportRow row in Summary(r))s.Append("<tr><td>"+H(row.Division)+"</td><td>"+H(row.Mu.ToString("N2",CultureInfo.InvariantCulture))+"</td><td>"+(row.ReducedMn.HasValue?H(row.ReducedMn.Value.ToString("N2",CultureInfo.InvariantCulture)):"—")+"</td><td>"+(row.ReducedCapacityDemandRatio.HasValue?H(row.ReducedCapacityDemandRatio.Value.ToString("0.0000",CultureInfo.InvariantCulture)):"—")+"</td><td>"+H(row.ReducedStatus)+"</td></tr>");
                s.Append("</table>");
            }
            foreach(string n in r.Notes)s.Append("<p>"+H(n)+"</p>");
            s.Append("<small>입력 SHA-256: "+Hash(p)+"</small><table><tr><th>Ag (mm²)</th><th>As (mm²)</th><th>Cx (mm)</th><th>Cy (mm)</th><th>Pmin (kN)</th><th>Pmax (kN)</th></tr><tr>");
            foreach(double d in new[]{r.GrossArea,r.SteelArea,r.Cx,r.Cy,r.PMin,r.PMax})s.Append("<td>"+H(d.ToString("F6",CultureInfo.InvariantCulture))+"</td>");s.Append("</tr></table>");
            s.Append("<h2>균형 변형률점</h2><p>εt=Fy/Es와 콘크리트 εcu의 동시 도달점. 기준별 압축지배·인장지배 판정과 다릅니다.</p><table><tr><th>축</th><th>Pb (kN)</th><th>Mbx (kN·m)</th><th>Mby (kN·m)</th><th>c (mm)</th><th>εt</th></tr>");
            for(int i=0;i<2;i++)foreach(CapacityPoint q in i==0?r.Limits.BalancedMx:r.Limits.BalancedMy)s.Append("<tr><td>"+(i==0?"P–Mx":"P–My")+"</td><td>"+H(q.P.ToString("F6",CultureInfo.InvariantCulture))+"</td><td>"+H(q.Mx.ToString("F6",CultureInfo.InvariantCulture))+"</td><td>"+H(q.My.ToString("F6",CultureInfo.InvariantCulture))+"</td><td>"+H(q.Depth.ToString("F6",CultureInfo.InvariantCulture))+"</td><td>"+H((-q.MinSteelStrain).ToString("G9",CultureInfo.InvariantCulture))+"</td></tr>");s.Append("</table>");
            s.Append("<h2>하중조합 — 동일 축력, 모멘트 방향 고정</h2><p>사용률은 공칭 모멘트 경계까지의 거리비입니다. 설계 안전율이 아닙니다.</p><table><tr><th>조합</th><th>P (kN)</th><th>Mx (kN·m)</th><th>My (kN·m)</th><th>공칭영역</th><th>모멘트 사용률</th></tr>");
            foreach(DemandResult d in r.Demands)s.Append("<tr><td>"+H(d.Name)+"</td><td>"+H(d.P)+"</td><td>"+H(d.Mx)+"</td><td>"+H(d.My)+"</td><td>"+H(d.Status)+"</td><td>"+(d.Utilization.HasValue?H(d.Utilization.Value.ToString("F6",CultureInfo.InvariantCulture)):"미정의")+"</td></tr>");
            s.Append("</table><p>방향 N/2N 비교의 최대 상대 변화: "+H(r.AngularChange.ToString("P4",CultureInfo.InvariantCulture))+"</p><h2>입력 원본 JSON</h2><pre>"+H(Files.Json(p))+"</pre></html>");
            File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
        }
    }
}
