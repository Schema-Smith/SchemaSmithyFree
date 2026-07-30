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
    }
}
