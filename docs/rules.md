# NpgsqlAnalyzers rules

|  Id  | Name | Severity | Description |
| ---- | :--- | :------- | :---------- |
| `PSCA1000` | Bad SQL statement | Warning | Reports SQL errors which are not explicitly handled by the analyzer. |
| `PSCA1001` | Undefined table | Warning | A table referenced in the SQL statement does not exist. Provides the name of the table. |
| `PSCA1002` | Undefined column | Warning | A column referenced in the SQL statement does not exist. Provides the name of the column. |
