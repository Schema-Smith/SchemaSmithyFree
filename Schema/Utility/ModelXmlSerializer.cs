// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Xml.Linq;
using Newtonsoft.Json;

namespace Schema.Utility
{
    /// <summary>
    /// Converts the parsed model's JSON (the single source of truth for the model shape — produced by
    /// <c>JArray.FromObject(...)</c> in <see cref="Schema.Domain.Template"/>) into the XML transport used by
    /// the legacy SQL Server ingest encoding. The XML is shredded with XQuery <c>.nodes()</c>/<c>.value()</c>
    /// (SQL Server 2005+) below the OPENJSON compat cliff. Converting the JSON rather than re-serializing the
    /// object graph keeps the XML shape automatically in step with the model as its properties evolve.
    /// </summary>
    public static class ModelXmlSerializer
    {
        /// <summary>
        /// Converts a JSON array of model objects (e.g. the table-definitions array) to ingest XML with the
        /// given root element. Each array element becomes a repeated <c>&lt;Item&gt;</c> child of the root;
        /// nested JSON arrays become repeated elements named after their JSON property.
        /// </summary>
        /// <param name="modelJsonArray">A JSON array string, e.g. <c>[{...},{...}]</c>.</param>
        /// <param name="rootElement">The XML root element name (e.g. <c>Tables</c>).</param>
        /// <param name="itemElement">The repeated per-element name (e.g. <c>Table</c>).</param>
        public static string ToIngestXml(string modelJsonArray, string rootElement, string itemElement)
        {
            // Wrap the array under the singular item property so Json.NET emits one repeated element per entry,
            // then give the whole thing the requested root element.
            var wrapped = "{\"" + itemElement + "\":" + modelJsonArray + "}";
            var doc = JsonConvert.DeserializeXNode(wrapped, rootElement);
            return doc!.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Converts a single JSON object (e.g. a kindling table definition) to XML rooted at
        /// <paramref name="rootElement"/>. Nested JSON arrays become repeated elements named by their property.
        /// </summary>
        /// <param name="modelJsonObject">A JSON object string, e.g. <c>{...}</c>.</param>
        /// <param name="rootElement">The XML root element name (e.g. <c>Table</c>).</param>
        public static string ToIngestXmlObject(string modelJsonObject, string rootElement)
        {
            var doc = JsonConvert.DeserializeXNode(modelJsonObject, rootElement);
            return doc!.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Inverse of <see cref="ToIngestXmlObject"/>: converts the compare-side ingest XML (emitted by the
        /// SQL Server <c>GenerateTableXml</c>/<c>GenerateIndexedViewXml</c> procs below the <c>FOR JSON</c>
        /// binary floor) back to a JSON object string that <c>PlatformDeserializer.DeserializeTable</c> can
        /// materialize into the domain model. The proc emits <c>json:Array="true"</c> (the Json.NET metadata
        /// namespace) on repeated containers so a single-element array does not collapse to an object; every
        /// scalar arrives as a JSON string (XML is typeless) and Newtonsoft coerces it into the typed model.
        /// </summary>
        /// <param name="tableXml">The XML for one object, rooted at a single element (e.g. <c>&lt;Table&gt;</c>).</param>
        public static string FromIngestXml(string tableXml)
        {
            var root = XElement.Parse(tableXml);
            return JsonConvert.SerializeXNode(root, Newtonsoft.Json.Formatting.None, omitRootObject: true);
        }
    }
}
