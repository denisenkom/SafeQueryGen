using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Text;
using System.Reflection;

namespace SafeQueryGen
{
	public enum OleDbFieldType
	{
		DBTYPE_I4,
		DBTYPE_R8,
		DBTYPE_BOOL,
		DBTYPE_DATE,
		DBTYPE_WVARCHAR,
		DBTYPE_WLONGVARCHAR,
		DBTYPE_CY,
		DBTYPE_VARBINARY,
        DBTYPE_VARCHAR,
        DBTYPE_DBTIMESTAMP,
        DBTYPE_LONGVARCHAR,
	}

	public class FieldTypeOleDBTypeMapper
	{
		private static OleDbFieldType[] _fieldTypes = {
			OleDbFieldType.DBTYPE_I4, OleDbFieldType.DBTYPE_R8,
			OleDbFieldType.DBTYPE_BOOL, OleDbFieldType.DBTYPE_DATE,
			OleDbFieldType.DBTYPE_WVARCHAR, OleDbFieldType.DBTYPE_WLONGVARCHAR,
			OleDbFieldType.DBTYPE_CY, OleDbFieldType.DBTYPE_VARBINARY,
            OleDbFieldType.DBTYPE_VARCHAR, OleDbFieldType.DBTYPE_DBTIMESTAMP,
            OleDbFieldType.DBTYPE_LONGVARCHAR,
		};
		private static OleDbType[] _oleDBTypes = {
			OleDbType.Integer, OleDbType.Double,
			OleDbType.Boolean, OleDbType.Date,
			OleDbType.VarWChar, OleDbType.LongVarWChar,
			OleDbType.Currency, OleDbType.VarBinary,
            OleDbType.VarChar, OleDbType.DBTimeStamp,
            OleDbType.LongVarChar,
		};

		public static OleDbType Map(OleDbFieldType fieldType)
		{
			Int32 i = 0;
			foreach (OleDbFieldType ft in _fieldTypes)
			{
				if (ft == fieldType)
				{
					return _oleDBTypes[i];
				}
				i++;
			}
			throw new ArgumentOutOfRangeException("fieldType", fieldType, "Нет синонима для указанного типа.");
		}

		public static OleDbFieldType Map(OleDbType fieldType)
		{
			Int32 i = 0;
			foreach (OleDbType ft in _oleDBTypes)
			{
				if (ft == fieldType)
				{
					return _fieldTypes[i];
				}
				i++;
			}
			throw new ArgumentOutOfRangeException("fieldType", fieldType, "Нет синонима для указанного типа.");
		}
	}

	public class ParserInfo
	{
		public CommandType CommandType;
		public List<OleDbFieldInfo> FieldsInfos = new List<OleDbFieldInfo>();
	}

	public class OleDbFieldInfo
	{
		public OleDbFieldInfo() { }

		public OleDbFieldInfo(OleDbFieldInfo orig)
		{
			foreach(System.Reflection.FieldInfo fi in typeof(OleDbFieldInfo).GetFields())
			{
				fi.SetValue(this, fi.GetValue(orig));
			}
		}

		public String FieldName;
		public Int32 Ordinal;
		public OleDbFieldType FieldType;
		public Int32 Size;
		public Int32 Precision;
		public Boolean IsNullable;
		public Boolean IsReadonly;
		public Boolean IsUnique;
		public Boolean IsKey;
		public Boolean IsAutoIncrement;
		public String BaseTableName;
		public String BaseColumnName;
	}

	class OleDbSqlParser
	{
		private OleDbConnection _connection;

		public OleDbSqlParser(String connectionString)
		{
			_connection = new OleDbConnection(connectionString);
			_connection.Open();
		}

		private void InitParameter(OleDbParameter parameter, Parameter parameterInfo)
		{
			parameter.ParameterName = parameterInfo.Name;
			parameter.OleDbType = parameterInfo.Type;
			//param.Direction = (ParameterDirection)Enum.Parse(typeof(ParameterDirection), paramNode.Attributes["direction"].Value);
			parameter.Size = parameterInfo.Size;
			switch (parameter.OleDbType)
			{
				case OleDbType.Date:
					parameter.Value = new DateTime();
					break;
				default:
					parameter.Value = 0;
					break;
			}
		}

		private List<OleDbFieldInfo> ExtractFieldsInfos(OleDbDataReader reader)
		{
			Int32 fieldsCount = reader.FieldCount;
			List<OleDbFieldInfo> result = new List<OleDbFieldInfo>(fieldsCount);
			if (fieldsCount > 0)
			{
				DataTable schemaTable = reader.GetSchemaTable();

                // это поле может отсутствовать в таблице, если так, то ishiddenindex == -1
                Int32 ishiddenindex = schemaTable.Columns.IndexOf("IsHidden");
				foreach (DataRow fieldSchema in schemaTable.Rows)
				{
					OleDbFieldInfo field = new OleDbFieldInfo();
					field.FieldName = fieldSchema["ColumnName"].ToString();
					if (field.FieldName == "")
						continue;
                    if (ishiddenindex != -1 && Boolean.Parse(fieldSchema[ishiddenindex].ToString()))
                        continue;
					field.Ordinal = Int32.Parse(fieldSchema["ColumnOrdinal"].ToString());
					field.FieldType = (OleDbFieldType)Enum.Parse(typeof(OleDbFieldType),
						reader.GetDataTypeName(field.Ordinal));
					field.Size = Int32.Parse(fieldSchema["ColumnSize"].ToString());
					field.Precision = Int32.Parse(fieldSchema["NumericPrecision"].ToString());
					field.IsNullable = Boolean.Parse(fieldSchema["AllowDBNull"].ToString());
					field.IsReadonly = Boolean.Parse(fieldSchema["IsReadonly"].ToString());
					field.IsUnique = Boolean.Parse(fieldSchema["IsUnique"].ToString());
					field.IsKey = Boolean.Parse(fieldSchema["IsKey"].ToString());
					field.IsAutoIncrement = Boolean.Parse(fieldSchema["IsAutoIncrement"].ToString());
					field.BaseTableName = fieldSchema["BaseTableName"].ToString();
					field.BaseColumnName = fieldSchema["BaseColumnName"].ToString();
					result.Add(field);
				}
			}
			return result;
		}

		public ParserInfo ParseSQL(String sql, CommandType commandType, List<Parameter> parametersInfos)
		{
			OleDbCommand command = new OleDbCommand(sql, _connection);
			OleDbDataReader reader;
			try
			{
				command.CommandType = commandType;
				//command.Prepare();
                if (parametersInfos != null)
                {
                    foreach (Parameter item in parametersInfos)
                    {
                        OleDbParameter oleparam = command.CreateParameter();
                        oleparam.OleDbType = item.Type;
                        oleparam.ParameterName = item.Name;
                        oleparam.Size = item.Size;
                        command.Parameters.Add(oleparam);
                    }
                }
				reader = command.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo);
			}
			catch (OleDbException ex)
			{
				throw new InvalidQueryException("Invalid query or parameters for query", sql, ex);
			}
			catch (InvalidOperationException ex)
			{
				throw new InvalidQueryException("Invalid query or parameters, unable to parse", sql, ex);
			}
			ParserInfo result = new ParserInfo();
			result.CommandType = command.CommandType;
			result.FieldsInfos = ExtractFieldsInfos(reader);
            reader.Close();
            command.Dispose();
			return result;
		}
	}
}
