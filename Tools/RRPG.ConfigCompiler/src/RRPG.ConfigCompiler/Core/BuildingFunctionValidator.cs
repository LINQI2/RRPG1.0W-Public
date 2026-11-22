using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace RRPG.ConfigCompiler.Core
{
    public static class BuildingFunctionValidator
    {
        public static void Validate(IReadOnlyList<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var table=sheets.FirstOrDefault(s=>s.Spec.SheetName=="BuildingFunctionBinding");
            if(table==null) return;
            void error(string message)=>bag.Error("DAT_BUILDING_FUNCTION_INVALID",message,DiagContext.Of(table.Spec.Workbook,table.Spec.SheetName,0));
            string text(CompiledRow r,string field)=>Convert.ToString(r.Get(field)?.Value,CultureInfo.InvariantCulture);
            foreach(string field in new[]{"BuildingDefinitionId","FunctionSlotId","FunctionType","FunctionProfileId"})
                if(!table.Spec.Fields.Any(f=>f.Field==field)) error("Missing field "+field);
            var definitions=sheets.FirstOrDefault(s=>s.Spec.SheetName=="BuildingDefinition");
            var ids=new HashSet<string>(definitions?.Rows.Select(r=>text(r,"BuildingDefinitionId")) ?? Enumerable.Empty<string>(),StringComparer.Ordinal);
            var slots=new HashSet<string>(StringComparer.Ordinal);
            foreach(var r in table.Rows)
            {
                string slot=text(r,"FunctionSlotId"),profile=text(r,"FunctionProfileId"),definition=text(r,"BuildingDefinitionId");
                if(!ids.Contains(definition) || string.IsNullOrWhiteSpace(slot) || slot.Contains(":") || !slots.Add(definition+":"+slot)
                    || text(r,"FunctionType")!="Workplace" || !new[]{"ws_farm_01","ws_kitchen_01","ws_test_01","ws_test_02"}.Contains(profile))
                    error("Invalid definition / slot / Workplace profile reference");
            }
        }
    }
}
