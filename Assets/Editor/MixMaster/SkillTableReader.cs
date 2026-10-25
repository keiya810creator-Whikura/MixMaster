#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MixMaster.EditorTools
{
    // No Excel application or third-party package is required.
    public static class SkillTableReader
    {
        public static List<Dictionary<string,string>> Read(string path)
        {
            List<List<string>> rows;
            if (Path.GetExtension(path).Equals(".xlsx",StringComparison.OrdinalIgnoreCase)) rows=ReadXlsx(path);
            else if (Path.GetExtension(path).Equals(".md",StringComparison.OrdinalIgnoreCase))
                rows=File.ReadAllLines(path).Where(l=>l.TrimStart().StartsWith("|") && !l.Contains("---")).Select(l=>l.Trim().Trim('|').Split('|').Select(x=>x.Trim().Trim('*')).ToList()).ToList();
            else rows=ReadCsv(File.ReadAllText(path,Encoding.UTF8));
            if(rows.Count==0)throw new InvalidDataException("表が空です。");
            var header=rows[0].Select(x=>x.Trim().Trim('\uFEFF')).ToList();
            if(header.Distinct().Count()!=header.Count)throw new InvalidDataException("列名が重複しています。");
            var result=new List<Dictionary<string,string>>();
            for(int r=1;r<rows.Count;r++)
            {
                var row=new Dictionary<string,string>();
                for(int c=0;c<header.Count;c++)row[header[c]]=c<rows[r].Count?rows[r][c].Trim():"";
                if(row.TryGetValue("ID",out string id)&&!string.IsNullOrWhiteSpace(id))result.Add(row);
            }
            return result;
        }
        private static List<List<string>> ReadXlsx(string path)
        {
            using(var stream=File.OpenRead(path))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))
            {
                XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XDocument Xml(string p){var e=zip.GetEntry(p)??throw new InvalidDataException("Excel内のファイルがありません: "+p);using(var s=e.Open())return XDocument.Load(s);}
                var book=Xml("xl/workbook.xml");
                var sheet=book.Descendants(ns+"sheet").FirstOrDefault(e=>(string)e.Attribute("name")=="スキル")??throw new InvalidDataException("「スキル」シートがありません。");
                string id=(string)sheet.Attribute(rel+"id");
                var relationships=Xml("xl/_rels/workbook.xml.rels");
                var link=relationships.Root.Elements().FirstOrDefault(e=>(string)e.Attribute("Id")==id)??throw new InvalidDataException("シート参照が不正です。");
                string target=(string)link.Attribute("Target");
                var uri=new Uri(new Uri("https://xlsx.local/xl/workbook.xml"),target);
                string sheetPath=uri.AbsolutePath.TrimStart('/');
                var strings=new List<string>();
                if(zip.GetEntry("xl/sharedStrings.xml")!=null)strings=Xml("xl/sharedStrings.xml").Root.Elements(ns+"si").Select(e=>string.Concat(e.Descendants(ns+"t").Select(t=>t.Value))).ToList();
                var rows=new List<List<string>>();
                foreach(var row in Xml(sheetPath).Descendants(ns+"row"))
                {
                    var cells=new List<string>();
                    foreach(var cell in row.Elements(ns+"c"))
                    {
                        string address=(string)cell.Attribute("r");int column=0;
                        foreach(char ch in address){if(!char.IsLetter(ch))break;column=column*26+(char.ToUpperInvariant(ch)-'A'+1);}
                        while(cells.Count<column)cells.Add("");
                        string type=(string)cell.Attribute("t");string v=cell.Element(ns+"v")?.Value??"";
                        if(type=="s")v=strings[int.Parse(v,CultureInfo.InvariantCulture)];
                        else if(type=="inlineStr")v=string.Concat(cell.Descendants(ns+"t").Select(e=>e.Value));
                        if(type=="e")throw new InvalidDataException("Excelの計算エラー: "+address+" "+v);
                        cells[column-1]=v;
                    }
                    rows.Add(cells);
                }
                return rows;
            }
        }
        public static List<List<string>> ReadCsv(string text)
        {
            var rows=new List<List<string>>();var row=new List<string>();var cell=new StringBuilder();bool quoted=false;
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];
                if(c=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}
                else if(!quoted&&(c==','||c=='\n'||c=='\r'))
                {row.Add(cell.ToString());cell.Clear();if(c!=','){rows.Add(row);row=new List<string>();if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;}}
                else cell.Append(c);
            }
            if(quoted)throw new InvalidDataException("CSVの引用符が閉じていません。");
            if(cell.Length>0||row.Count>0){row.Add(cell.ToString());rows.Add(row);}return rows;
        }
    }
}
#endif
