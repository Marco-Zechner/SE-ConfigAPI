using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private static bool AssignKeyPath(TomlTable startTable, List<TomlKeyPart> parts, TomlNode value, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            var table = startTable;

            for (var i = 0; i < parts.Count - 1; i++)
            {
                var part = parts[i];
                TomlNode existing;

                if (!table.TryGetValue(part.Value, out existing))
                {
                    var created = new TomlTable(
                        part.Line,
                        part.Column,
                        TomlTableDefinitionKind.DottedKey);

                    table.Set(part.Value, created);
                    table = created;
                    continue;
                }

                if (existing.Kind != TomlNodeKind.Table)
                {
                    diagnostic = Error($"Key '{part.Value}' is already defined as a value and cannot be used as a table.", 
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                var existingTable = (TomlTable)existing;

                switch (existingTable.DefinitionKind)
                {
                    case TomlTableDefinitionKind.Inline:
                        diagnostic = Error($"Inline table '{part.Value}' is immutable and cannot be extended through a dotted key.", 
                            part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                        return false;
                    
                    case TomlTableDefinitionKind.Explicit:
                        diagnostic = Error($"Table '{part.Value}' was already explicitly defined and cannot be extended through a dotted key.", 
                            part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                        return false;
                    
                    default:
                        table = existingTable;
                        break;
                }
            }

            var finalPart = parts[parts.Count - 1];

            if (table.ContainsKey(finalPart.Value))
            {
                diagnostic = Error($"The key '{finalPart.Value}' is already defined.", 
                    finalPart.Line, finalPart.Column, TomlDiagnosticCode.DuplicateKey);
                return false;
            }

            table.Set(finalPart.Value, value);
            return true;
        }
    }
}
