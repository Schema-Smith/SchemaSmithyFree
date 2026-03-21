namespace Schema.Domain;

public enum TemplateQuenchSlot : ushort
{
    Before,
    Objects,
    BetweenTablesAndKeys,
    AfterTablesScripts,
    AfterTablesObjects,
    TableData,
    After,
    None
}
