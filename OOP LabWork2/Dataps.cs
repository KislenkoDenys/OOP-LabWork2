using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace OOP_LabWork2
{
    public interface IXml
    {
        string Name { get; }
        Dictionary<string, List<string>> GetFilterAttributes(string xmlContent);
        string Search(string xmlContent, Dictionary<string, string> searchCriteria);
    }

    public class Sax : IXml
    {
        public string Name => "SAX API";
        private struct ScientistData
        {
            public string Id { get; set; }
            public string Faculty { get; set; }
            public string Department { get; set; }
            public string DegreeType { get; set; }
            public string DegreeValue { get; set; }
            public string FullName { get; set; }
            public List<(string Title, string Date)> Ranks { get; set; }
        }
        public Dictionary<string, List<string>> GetFilterAttributes(string xmlContent)
        {
            var attributes = new Dictionary<string, List<string>>
            {
                { "Faculty", new List<string>() },
                { "Department", new List<string>() },
                { "DegreeType", new List<string>() },
                { "Rank", new List<string>() }
            };
            var faculties = new HashSet<string>();
            var departments = new HashSet<string>();
            var degrees = new HashSet<string>();
            var ranks = new HashSet<string>();

            using (var reader = XmlReader.Create(new StringReader(xmlContent)))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "Faculty") faculties.Add(reader.ReadElementContentAsString());
                        else if (reader.Name == "Department") departments.Add(reader.ReadElementContentAsString());
                        else if (reader.Name == "Degree" && reader.MoveToAttribute("type"))
                        {
                            degrees.Add(reader.Value);
                            reader.MoveToElement();
                        }
                        else if (reader.Name == "Rank" && reader.MoveToAttribute("title"))
                        {
                            ranks.Add(reader.Value);
                            reader.MoveToElement();
                        }
                    }
                }
            }
            attributes["Faculty"] = faculties.OrderBy(x => x).ToList();
            attributes["Department"] = departments.OrderBy(x => x).ToList();
            attributes["DegreeType"] = degrees.OrderBy(x => x).ToList();
            attributes["Rank"] = ranks.OrderBy(x => x).ToList();
            return attributes;
        }

        public string Search(string xmlContent, Dictionary<string, string> searchCriteria)
        {
            var res = new List<ScientistData>();
            ScientistData curS = new ScientistData();
            string cur = string.Empty;
            bool isinsideS = false;

            using (var reader = XmlReader.Create(new StringReader(xmlContent)))
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            cur = reader.Name;
                            if (cur == "Scientist")
                            {
                                isinsideS = true;
                                curS = new ScientistData { Ranks = new List<(string, string)>() };
                                if (reader.MoveToAttribute("id")) curS.Id = reader.Value;
                            }
                            else if (isinsideS && cur == "Degree")
                            {
                                if (reader.MoveToAttribute("type")) curS.DegreeType = reader.Value;
                            }
                            else if (isinsideS && cur == "Rank")
                            {
                                string title = reader.GetAttribute("title") ?? "";
                                string date = reader.GetAttribute("date") ?? "";
                                curS.Ranks.Add((title, date));
                            }
                            break;

                        case XmlNodeType.Text:
                            if (!isinsideS) break;
                            string content = reader.Value.Trim();
                            if (string.IsNullOrEmpty(content)) break;

                            if (cur == "FullName") curS.FullName = content;
                            else if (cur == "Faculty") curS.Faculty = content;
                            else if (cur == "Department") curS.Department = content;
                            else if (cur == "Degree") curS.DegreeValue = content;
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name == "Scientist")
                            {
                                isinsideS = false;
                                bool match = true;
                                foreach (var criterion in searchCriteria)
                                {
                                    string searchVal = criterion.Value; // Те, що ввів користувач

                                    // Використовуємо Helper для перевірки на входження без урахування регістру
                                    if (criterion.Key == "Faculty" &&
                                        (curS.Faculty == null || !curS.Faculty.Contains(searchVal, StringComparison.OrdinalIgnoreCase))) match = false;

                                    else if (criterion.Key == "Department" &&
                                        (curS.Department == null || !curS.Department.Contains(searchVal, StringComparison.OrdinalIgnoreCase))) match = false;

                                    else if (criterion.Key == "DegreeType" &&
                                        (curS.DegreeType == null || !curS.DegreeType.Contains(searchVal, StringComparison.OrdinalIgnoreCase))) match = false;

                                    else if (criterion.Key == "Rank" &&
                                        !curS.Ranks.Any(r => r.Title.Contains(searchVal, StringComparison.OrdinalIgnoreCase))) match = false;
                                }
                                if (match) res.Add(curS);
                            }
                            break;
                    }
                }
            }
            return toXml(res);
        }

        private string toXml(List<ScientistData> results)
        {
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
            var sw = new StringWriter();

            var writer = XmlWriter.Create(sw, settings);

            writer.WriteStartElement("ScientistsResults");
            foreach (var scientist in results)
            {
                writer.WriteStartElement("Scientist");
                writer.WriteAttributeString("id", scientist.Id);
                writer.WriteElementString("FullName", scientist.FullName);
                writer.WriteElementString("Faculty", scientist.Faculty);
                writer.WriteElementString("Department", scientist.Department);

                writer.WriteStartElement("Degree");
                writer.WriteAttributeString("type", scientist.DegreeType);
                writer.WriteString(scientist.DegreeValue);
                writer.WriteEndElement();

                writer.WriteStartElement("Ranks");
                foreach (var rank in scientist.Ranks)
                {
                    writer.WriteStartElement("Rank");
                    writer.WriteAttributeString("title", rank.Title);
                    writer.WriteAttributeString("date", rank.Date);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.Flush();

            return sw.ToString();

        }
    }

    public class Dom : IXml
    {
        public string Name => "DOM API";

        public Dictionary<string, List<string>> GetFilterAttributes(string xmlContent)
        {
            var attributes = new Dictionary<string, List<string>> {
                { "Faculty", new List<string>() }, { "Department", new List<string>() },
                { "DegreeType", new List<string>() }, { "Rank", new List<string>() }
            };

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);

            attributes["Faculty"] = GetUniqueValues(xmlDoc, "//Faculty");
            attributes["Department"] = GetUniqueValues(xmlDoc, "//Department");
            attributes["DegreeType"] = GetUniqueAttributeValues(xmlDoc, "//Degree", "type");
            attributes["Rank"] = GetUniqueAttributeValues(xmlDoc, "//Rank", "title");

            return attributes;
        }

        private List<string> GetUniqueValues(XmlDocument doc, string xpath) =>
            doc.SelectNodes(xpath)?.Cast<XmlNode>().Select(n => n.InnerText).Distinct().OrderBy(x => x).ToList() ?? new List<string>();

        private List<string> GetUniqueAttributeValues(XmlDocument doc, string xpath, string attr) =>
            doc.SelectNodes(xpath)?.Cast<XmlNode>().Select(n => n.Attributes[attr]?.Value).Where(v => v != null).Distinct().OrderBy(x => x).ToList() ?? new List<string>();

        public string Search(string xmlContent, Dictionary<string, string> searchCriteria)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            var res = new XmlDocument();
            var root = res.CreateElement("ScientistsResults");
            res.AppendChild(root);
            var scientists = xmlDoc.SelectNodes("//Scientists/Scientist");
            if (scientists != null)
            {
                foreach (XmlNode node in scientists)
                {
                    bool isMatch = true;
                    foreach (var crit in searchCriteria)
                    {
                        string valueToCheck = string.Empty;
                        if (crit.Key == "DegreeType")
                        {
                            var degreeNode = node.SelectSingleNode("Degree");
                            valueToCheck = degreeNode?.Attributes["type"]?.Value ?? "";
                        }
                        else if (crit.Key == "Rank")
                        {
                            var rankNodes = node.SelectNodes("Ranks/Rank");
                            bool rankFound = false;
                            if (rankNodes != null)
                            {
                                foreach (XmlNode rank in rankNodes)
                                {
                                    string rankTitle = rank.Attributes["title"]?.Value ?? "";
                                    if (rankTitle.Contains(crit.Value, StringComparison.OrdinalIgnoreCase))
                                    {
                                        rankFound = true;
                                        break;
                                    }
                                }
                            }
                            if (!rankFound)
                            {
                                isMatch = false;
                                break;
                            }
                            continue;
                        }
                        else
                        {
                            var targetNode = node.SelectSingleNode(crit.Key);
                            valueToCheck = targetNode?.InnerText ?? "";
                        }
                        if (!valueToCheck.Contains(crit.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        root.AppendChild(res.ImportNode(node, true));
                    }
                }
            }

            return res.OuterXml;
        }
    }

    public class LinqToXml : IXml
    {
        public string Name => "LINQ";

        public Dictionary<string, List<string>> GetFilterAttributes(string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent);
            return new Dictionary<string, List<string>>
            {
                { "Faculty", doc.Descendants("Faculty").Select(e => e.Value).Distinct().OrderBy(v => v).ToList() },
                { "Department", doc.Descendants("Department").Select(e => e.Value).Distinct().OrderBy(v => v).ToList() },
                { "DegreeType", doc.Descendants("Degree").Select(e => e.Attribute("type")?.Value).Where(v => v != null).Distinct().OrderBy(v => v).ToList() },
                { "Rank", doc.Descendants("Rank").Select(e => e.Attribute("title")?.Value).Where(v => v != null).Distinct().OrderBy(v => v).ToList() }
            };
        }

        public string Search(string xmlContent, Dictionary<string, string> criteria)
        {
            var doc = XDocument.Parse(xmlContent);
            var query = doc.Descendants("Scientist").AsEnumerable();

            if (criteria.ContainsKey("Faculty"))
                query = query.Where(s => s.Element("Faculty")?.Value
                    .Contains(criteria["Faculty"], StringComparison.OrdinalIgnoreCase) == true);

            if (criteria.ContainsKey("Department"))
                query = query.Where(s => s.Element("Department")?.Value
                    .Contains(criteria["Department"], StringComparison.OrdinalIgnoreCase) == true);

            if (criteria.ContainsKey("DegreeType"))
                query = query.Where(s => s.Element("Degree")?.Attribute("type")?.Value
                    .Contains(criteria["DegreeType"], StringComparison.OrdinalIgnoreCase) == true);

            if (criteria.ContainsKey("Rank"))
                query = query.Where(s => s.Descendants("Rank")
                    .Any(r => r.Attribute("title")?.Value
                    .Contains(criteria["Rank"], StringComparison.OrdinalIgnoreCase) == true));

            var resultsDoc = new XDocument(new XElement("ScientistsResults", query));
            return resultsDoc.ToString();
        }
    }

    public class XslTransformer
    {
        public string XmltoHTML(string xmlContent, string xslContent)
        {
            try
            {
                using var xmlReader = XmlReader.Create(new StringReader(xmlContent));
                using var xslReader = XmlReader.Create(new StringReader(xslContent));
                var xslt = new XslCompiledTransform();
                xslt.Load(xslReader);
                using var sw = new StringWriter();
                using var xw = XmlWriter.Create(sw, xslt.OutputSettings);
                xslt.Transform(xmlReader, xw);
                return sw.ToString();
            }
            catch (Exception ex)
            {
                return $"<html><body><h1>Error</h1><pre>{ex.Message}</pre></body></html>";
            }
        }
    }
}