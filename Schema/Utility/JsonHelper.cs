using Schema.Isolators;
using Newtonsoft.Json;
using System;

namespace Schema.Utility
{
    public static class JsonHelper
    {
        public static T Load<T>(string filePath)
        {
            if (!FileWrapper.GetFromFactory().Exists(filePath))
                throw new Exception($"File {filePath} does not exist");

            var text = FileWrapper.GetFromFactory().ReadAllText(filePath);
            var schema = JsonConvert.DeserializeObject<T>(text);
            return schema;
        }

        public static T ProductLoad<T>(string filePath)
        {
            if (!ProductFileWrapper.GetFromFactory().Exists(filePath))
                throw new Exception($"File {filePath} does not exist");

            var text = ProductFileWrapper.GetFromFactory().ReadAllText(filePath);
            var schema = JsonConvert.DeserializeObject<T>(text);
            return schema;
        }

        public static void Write<T>(string filePath, T obj)
        {
            var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };
            FileWrapper.GetFromFactory().WriteAllText(filePath, JsonConvert.SerializeObject(obj, settings));
        }
    }
}
