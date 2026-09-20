using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ResolveTableHeader(IList<TomlKeyPart> parts, out TomlTable result, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;
            result = null;

            var table = _root;

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var isLeaf = i == parts.Count - 1;

                TomlNode existing;

                if (!table.TryGetValue(part.Value, out existing))
                {
                    var created = new TomlTable(part.Line, part.Column, isLeaf ? TomlTableDefinitionKind.Explicit : TomlTableDefinitionKind.Implicit);

                    table.Set(part.Value, created);
                    table = created;
                    continue;
                }

                if (existing.Kind == TomlNodeKind.Array)
                {
                    var array = (TomlArray)existing;

                    if (array.DefinitionKind != TomlArrayDefinitionKind.ArrayOfTables)
                    {
                        diagnostic = Error($"Key '{part.Value}' is already defined as a static array and cannot be used as a table.",
                            part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                        return false;
                    }

                    if (isLeaf)
                    {
                        diagnostic = Error($"Array of tables '{part.Value}' cannot be redefined as a standard table.",
                            part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                        return false;
                    }

                    if (!TryGetLatestArrayTable(array, part, out table, out diagnostic))
                        return false;

                    continue;
                }

                if (existing.Kind != TomlNodeKind.Table)
                {
                    diagnostic = Error($"Key '{part.Value}' is already defined as a value and cannot be used as a table.",
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                var existingTable = (TomlTable)existing;

                if (existingTable.DefinitionKind == TomlTableDefinitionKind.Inline)
                {
                    diagnostic = Error($"Inline table '{part.Value}' is immutable and cannot be extended or redefined by a table header.",
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                if (isLeaf)
                {
                    switch (existingTable.DefinitionKind)
                    {
                        case TomlTableDefinitionKind.Implicit:
                            existingTable.DefinitionKind = TomlTableDefinitionKind.Explicit;
                            break;
                        case TomlTableDefinitionKind.DottedKey:
                            diagnostic = Error($"Table '{part.Value}' was already defined by a dotted key and cannot be redefined by a table header.",
                                part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                            return false;
                        default:
                            diagnostic = Error($"Table '{part.Value}' is already explicitly defined.",
                                part.Line, part.Column, TomlDiagnosticCode.DuplicateTable);
                            return false;
                    }
                }

                table = existingTable;
            }

            result = table;
            return true;
        }

        private bool ResolveArrayTableHeader(IList<TomlKeyPart> parts, out TomlTable result, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;
            result = null;

            var table = _root;

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var isLeaf = i == parts.Count - 1;

                TomlNode existing;

                if (!table.TryGetValue(part.Value, out existing))
                {
                    if (isLeaf)
                    {
                        var array = new TomlArray(part.Line, part.Column, TomlArrayDefinitionKind.ArrayOfTables);

                        var element = new TomlTable(part.Line, part.Column, TomlTableDefinitionKind.Explicit);

                        array.Add(element);
                        table.Set(part.Value, array);

                        result = element;
                        return true;
                    }

                    var created = new TomlTable(part.Line, part.Column, TomlTableDefinitionKind.Implicit);

                    table.Set(part.Value, created);
                    table = created;
                    continue;
                }

                if (existing.Kind == TomlNodeKind.Array)
                {
                    var array = (TomlArray)existing;

                    if (array.DefinitionKind != TomlArrayDefinitionKind.ArrayOfTables)
                    {
                        diagnostic = Error($"Key '{part.Value}' is already defined as a static array and cannot become an array of tables.",
                            part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                        return false;
                    }

                    if (isLeaf)
                    {
                        var element = new TomlTable(part.Line, part.Column, TomlTableDefinitionKind.Explicit);

                        array.Add(element);

                        result = element;
                        return true;
                    }

                    if (!TryGetLatestArrayTable(array, part, out table, out diagnostic))
                        return false;

                    continue;
                }

                if (existing.Kind != TomlNodeKind.Table)
                {
                    diagnostic = Error($"Key '{part.Value}' is already defined as a value and cannot become an array of tables.",
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                var existingTable = (TomlTable)existing;

                if (existingTable.DefinitionKind == TomlTableDefinitionKind.Inline)
                {
                    diagnostic = Error($"Inline table '{part.Value}' is immutable and cannot be extended by an array-of-tables header.",
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                if (isLeaf)
                {
                    diagnostic = Error($"Table '{part.Value}' is already defined and cannot become an array of tables.",
                        part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                    return false;
                }

                table = existingTable;
            }

            diagnostic = Error("Array-of-tables header did not resolve to a table.", _line, _column, TomlDiagnosticCode.InvalidTable);

            return false;
        }

        private static bool TryGetLatestArrayTable(TomlArray array, TomlKeyPart part, out TomlTable table, out TomlDiagnostic diagnostic)
        {
            table = null;
            diagnostic = null;

            if (array.Count == 0)
            {
                diagnostic = Error($"Array of tables '{part.Value}' has no table element to extend.",
                    part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                return false;
            }

            var latest = array[array.Count - 1];

            if (latest.Kind != TomlNodeKind.Table)
            {
                diagnostic = Error($"Array of tables '{part.Value}' contains a non-table element.",
                    part.Line, part.Column, TomlDiagnosticCode.TableConflict);
                return false;
            }

            table = (TomlTable)latest;
            return true;
        }
    }
}
