using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace OOP_LabWork2
{
    public interface IXmlAnalysisStrategy
    {
        string Name { get; }
        Dictionary<string, List<string>> GetFilterAttributes(string xmlContent);
        string Search(string xmlContent, Dictionary<string, string> searchCriteria);
    }

    public class SaxAnalysisStrategy : IXmlAnalysisStrategy
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
            var results = new List<ScientistData>();
            ScientistData currentScientist = new ScientistData();
            string currentElement = string.Empty;
            bool isInsideScientist = false;

            using (var reader = XmlReader.Create(new StringReader(xmlContent)))
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            currentElement = reader.Name;
                            if (currentElement == "Scientist")
                            {
                                isInsideScientist = true;
                                currentScientist = new ScientistData { Ranks = new List<(string, string)>() };
                                if (reader.MoveToAttribute("id")) currentScientist.Id = reader.Value;
                            }
                            else if (isInsideScientist && currentElement == "Degree")
                            {
                                if (reader.MoveToAttribute("type")) currentScientist.DegreeType = reader.Value;
                            }
                            else if (isInsideScientist && currentElement == "Rank")
                            {
                                string title = reader.GetAttribute("title") ?? "";
                                string date = reader.GetAttribute("date") ?? "";
                                currentScientist.Ranks.Add((title, date));
                            }
                            break;

                        case XmlNodeType.Text:
                            if (!isInsideScientist) break;
                            string content = reader.Value.Trim();
                            if (string.IsNullOrEmpty(content)) break;

                            if (currentElement == "FullName") currentScientist.FullName = content;
                            else if (currentElement == "Faculty") currentScientist.Faculty = content;
                            else if (currentElement == "Department") currentScientist.Department = content;
                            else if (currentElement == "Degree") currentScientist.DegreeValue = content;
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name == "Scientist")
                            {
                                isInsideScientist = false;
                                bool match = true;
                                foreach (var criterion in searchCriteria)
                                {
                                    if (criterion.Key == "Faculty" && currentScientist.Faculty?.Trim() != criterion.Value) match = false;
                                    else if (criterion.Key == "Department" && currentScientist.Department?.Trim() != criterion.Value) match = false;
                                    else if (criterion.Key == "DegreeType" && currentScientist.DegreeType?.Trim() != criterion.Value) match = false;
                                    else if (criterion.Key == "Rank" && !currentScientist.Ranks.Any(r => r.Title.Trim() == criterion.Value)) match = false;
                                }
                                if (match) results.Add(currentScientist);
                            }
                            break;
                    }
                }
            }
            return ConvertResultsToXml(results);
        }

        private string ConvertResultsToXml(List<ScientistData> results)
        {
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
            using (var sw = new StringWriter())
            {
                using (var writer = XmlWriter.Create(sw, settings))
                {
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
                }
                return sw.ToString();
            }
        }
    }

    public class DomAnalysisStrategy : IXmlAnalysisStrategy
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
            string xpath = "//Scientists/Scientist";
            foreach (var crit in searchCriteria)
            {
                if (crit.Key == "DegreeType")
                    xpath += $"[Degree/@type=\"{crit.Value}\"]";
                else if (crit.Key == "Rank")
                    xpath += $"[Ranks/Rank/@title=\"{crit.Value}\"]";
                else
                    xpath += $"[{crit.Key}=\"{crit.Value}\"]";
            }

            var filteredNodes = xmlDoc.SelectNodes(xpath);
            var resultsDoc = new XmlDocument();
            resultsDoc.AppendChild(resultsDoc.CreateElement("ScientistsResults"));

            if (filteredNodes != null)
            {
                foreach (XmlNode node in filteredNodes)
                {
                    resultsDoc.DocumentElement?.AppendChild(resultsDoc.ImportNode(node, true));
                }
            }
            return resultsDoc.OuterXml;
        }
    }

    public class LinqToXmlStrategy : IXmlAnalysisStrategy
    {
        public string Name => "LINQ to XML";

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

        public string Search(string xmlContent, Dictionary<string, string> searchCriteria)
        {
            var doc = XDocument.Parse(xmlContent);
            var query = doc.Descendants("Scientist").AsEnumerable();

            if (searchCriteria.ContainsKey("Faculty"))
                query = query.Where(s => s.Element("Faculty")?.Value == searchCriteria["Faculty"]);
            if (searchCriteria.ContainsKey("Department"))
                query = query.Where(s => s.Element("Department")?.Value == searchCriteria["Department"]);
            if (searchCriteria.ContainsKey("DegreeType"))
                query = query.Where(s => s.Element("Degree")?.Attribute("type")?.Value == searchCriteria["DegreeType"]);
            if (searchCriteria.ContainsKey("Rank"))
                query = query.Where(s => s.Descendants("Rank").Any(r => r.Attribute("title")?.Value == searchCriteria["Rank"]));

            var resultsDoc = new XDocument(new XElement("ScientistsResults", query));
            return resultsDoc.ToString();
        }
    }

    public class XslTransformer
    {
        public string TransformXmlToHtml(string xmlContent, string xslContent)
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