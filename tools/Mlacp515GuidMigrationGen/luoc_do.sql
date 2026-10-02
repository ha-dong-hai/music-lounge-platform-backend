-- MLACP-515: in luoc do dbo de so DB da migrate voi DB tao thang tu model. Moi dong mot dac diem, sap xep on dinh.
SET NOCOUNT ON;
SELECT x FROM (
  SELECT N'col ' + t.name + N'.' + c.name + N' ' + ty.name
       + CASE WHEN ty.name IN (N'nvarchar', N'varchar', N'varbinary') THEN N'(' + CASE WHEN c.max_length = -1 THEN N'max' ELSE CAST(c.max_length AS nvarchar) END + N')'
              WHEN ty.name IN (N'decimal') THEN N'(' + CAST(c.precision AS nvarchar) + N',' + CAST(c.scale AS nvarchar) + N')' ELSE N'' END
       + CASE WHEN c.is_nullable = 1 THEN N' null' ELSE N' notnull' END + CASE WHEN c.is_identity = 1 THEN N' identity' ELSE N'' END AS x
  FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name <> N'__EFMigrationsHistory'
  UNION ALL
  SELECT N'ix ' + t.name + N' ' + i.name + CASE WHEN i.is_primary_key = 1 THEN N' PK' WHEN i.is_unique = 1 THEN N' UQ' ELSE N'' END
       + N' (' + STUFF((SELECT N',' + c.name FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0 ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 1, N'') + N')'
       + ISNULL(N' WHERE ' + i.filter_definition COLLATE DATABASE_DEFAULT, N'')
  FROM sys.tables t JOIN sys.indexes i ON i.object_id = t.object_id
  WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name <> N'__EFMigrationsHistory' AND i.index_id > 0
  UNION ALL
  SELECT N'fk ' + OBJECT_NAME(fk.parent_object_id) + N' ' + fk.name + N' (' +
         STUFF((SELECT N',' + c.name FROM sys.foreign_key_columns fc JOIN sys.columns c ON c.object_id = fc.parent_object_id AND c.column_id = fc.parent_column_id
                WHERE fc.constraint_object_id = fk.object_id ORDER BY fc.constraint_column_id FOR XML PATH('')), 1, 1, N'')
       + N') -> ' + OBJECT_NAME(fk.referenced_object_id) + N' ' + fk.delete_referential_action_desc
  FROM sys.foreign_keys fk WHERE fk.schema_id = SCHEMA_ID('dbo')
  UNION ALL
  SELECT N'df ' + OBJECT_NAME(d.parent_object_id) + N'.' + c.name + N' = ' + d.definition COLLATE DATABASE_DEFAULT
  FROM sys.default_constraints d JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
  WHERE d.schema_id = SCHEMA_ID('dbo')
) q ORDER BY x;
