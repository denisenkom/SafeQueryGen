using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Text;

namespace SafeQueryGen
{
	class BuilderClasses
	{
		private BuilderClasses() { }

		public const String TIntegerField = "Db::TIntegerField";
		public const String TFloatField = "Db::TFloatField";
		public const String TBooleanField = "Db::TBooleanField";
		public const String TBCDField = "Db::TBCDField";
		public const String TCurrencyField = "Db::TCurrencyField";
		public const String TDateTimeField = "Db::TDateTimeField";
		public const String TMemoField = "Db::TMemoField";
		public const String TStringField = "Db::TStringField";
		public const String TWideStringField = "Db::TWideStringField";
		public const String TBlobField = "Db::TBlobField";
		public const String TADOQuery = "Adodb::TADOQuery";
		public const String TADOTable = "Adodb::TADOTable";
		public const String TADOCommand = "Adodb::TADOCommand";
		public const String TCustomADODataSet = "Adodb::TCustomADODataSet";
		public const String TADODataSet = "Adodb::TADODataSet";
		public const String TADOConnection = "Adodb::TADOConnection";
		public const String TParameter = "Adodb::TParameter";
	}

	struct BuilderFieldType
	{
		public String FieldTypeName;
		public String CppTypeName;

		public BuilderFieldType(String fieldTypeName, String cppTypeName)
		{
			FieldTypeName = fieldTypeName;
			CppTypeName = cppTypeName;
		}
	}

	class Convertions
	{
		private Convertions() { }

		public static BuilderFieldType FromFieldType(OleDbFieldType type)
		{
			switch (type)
			{
				case OleDbFieldType.DBTYPE_I4:
					return new BuilderFieldType(BuilderClasses.TIntegerField, "int");
				case OleDbFieldType.DBTYPE_R8:
					return new BuilderFieldType(BuilderClasses.TFloatField, "double");
				case OleDbFieldType.DBTYPE_BOOL:
					return new BuilderFieldType(BuilderClasses.TBooleanField, "bool");
				case OleDbFieldType.DBTYPE_CY: // builder bug: builders TCurrencyField maps to TFloatField, using TBCDField
					return new BuilderFieldType(BuilderClasses.TBCDField, "Currency");
				case OleDbFieldType.DBTYPE_DATE:
					return new BuilderFieldType(BuilderClasses.TDateTimeField, "TDateTime");
				case OleDbFieldType.DBTYPE_WLONGVARCHAR:
                    return new BuilderFieldType(BuilderClasses.TMemoField, "UnicodeString");
				case OleDbFieldType.DBTYPE_WVARCHAR:
                    return new BuilderFieldType(BuilderClasses.TWideStringField, "UnicodeString");
				case OleDbFieldType.DBTYPE_VARBINARY:
					return new BuilderFieldType(BuilderClasses.TBlobField, "AnsiString");
                case OleDbFieldType.DBTYPE_VARCHAR:
                    return new BuilderFieldType(BuilderClasses.TStringField, "AnsiString");
                case OleDbFieldType.DBTYPE_DBTIMESTAMP:
                    return new BuilderFieldType(BuilderClasses.TDateTimeField, "TDateTime");
                case OleDbFieldType.DBTYPE_LONGVARCHAR:
                    return new BuilderFieldType(BuilderClasses.TMemoField, "AnsiString");
				default:
					throw new NotImplementedException();
			}
		}

		public static String ParamTypeFromOleDbType(OleDbType value)
		{
			switch (value)
			{
				case OleDbType.Integer:
					return "ftInteger";
				case OleDbType.VarChar:
					return "ftWideString";
				/*case OleDbType.Decimal:
					return "ftBCD";*/
				case OleDbType.Currency:
					return "ftBCD"; // builder bug: builder maps ftCurrency to ftDouble
				case OleDbType.Variant:
					return "ftVariant";
				case OleDbType.Date:
					return "ftDate";
				case OleDbType.Boolean:
					return "ftBoolean";
				case OleDbType.Binary:
					return "ftBlob";
				default:
					throw new NotImplementedException();
			}
		}

		public static String CppTypeFromOleDbType(OleDbType value)
		{
			switch (value)
			{
				case OleDbType.Integer:
					return "int";
				case OleDbType.VarChar:
					return "WideString";
				/*case OleDbType.Decimal:
					return "Currency";*/
				case OleDbType.Variant:
					return "Variant";
				case OleDbType.Currency:
					return "Currency";
				case OleDbType.Date:
					return "TDateTime";
				case OleDbType.Boolean:
					return "bool";
				case OleDbType.Binary:
					return "AnsiString";
				default:
					throw new NotImplementedException();
			}
		}

		public static String CppTypeFromCtsType(Type ctsType)
		{
			if (ctsType == typeof(Int32))
				return "int";
			else if (ctsType == typeof(String))
				return "WideString";
			else if (ctsType == typeof(Double))
				return "double";
			else if (ctsType == typeof(Decimal))
				return "Currency";
			else
				throw new NotImplementedException();
		}

		public static String FromParamDirection(ParameterDirection value)
		{
			switch (value)
			{
				case ParameterDirection.Input:
					return "pdInput";
				case ParameterDirection.Output:
					return "pdOutput";
				case ParameterDirection.InputOutput:
					return "pdInputOutput";
				case ParameterDirection.ReturnValue:
					return "pdReturnValue";
				default:
					throw new NotImplementedException();
			}
		}

		public static String DecorateParameterType(String parameterType)
		{
			if (parameterType == "WideString" || parameterType == "Variant")
			{
				return "const " + parameterType + "&";
			}
			else
			{
				return parameterType;
			}
		}
	}
}
