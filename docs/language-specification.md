# Supported Structured Text Subset — v0.1

Implemented:

- `PROGRAM ... END_PROGRAM`
- `VAR ... END_VAR`
- `BOOL`, `INT`
- Boolean literals: `TRUE`, `FALSE`
- Assignment: `:=`
- Arithmetic: `+ - * /`
- Comparisons: `= <> < <= > >=`
- Boolean operators: `AND OR NOT`
- `IF / THEN / ELSE / END_IF`
- IEC-style block comments: `(* ... *)`

Planned:

- `WHILE`, `FOR`, `CASE`
- `REAL`, `TIME`, arrays and enums
- `FUNCTION` and `FUNCTION_BLOCK`
- Standard timers such as `TON`
- I/O address syntax (`%IX`, `%QX`)
- IEC 61131-3 conformance expansion
