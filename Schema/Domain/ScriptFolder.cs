using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;

namespace Schema.Domain
{
    public class ScriptFolder
    {
        public string FolderPath { get; set; }
        public QuenchSlot QuenchSlot { get; set; }
        public List<SqlScript> Scripts { get; set; } = [];

        public void LoadSqlFiles(string basePath)
        {
            var sqlFilePath = Path.Combine(basePath, FolderPath);
            if (!DirectoryWrapper.GetFromFactory().Exists(sqlFilePath)) return;

            var files = DirectoryWrapper.GetFromFactory().GetFiles(sqlFilePath, "*.sql", SearchOption.AllDirectories).OrderBy(x => x);
            Scripts.AddRange(files.Select(SqlScript.Load));
        }
    }
}
