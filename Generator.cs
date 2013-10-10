using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace SafeQueryGen
{
	public class Generator : CppBuilderWriter
	{
		private String _globalConnection = null;
		private SafeQueryGenDocument _sourceDocument;
		private List<String> _classes = new List<String>();

		public void Generate(String inputFile, String outputFile)
		{
			_sourceDocument = SafeQueryGenDocument.Deserialize(inputFile);
			Context.CurrentContext.Document = _sourceDocument;
			try
			{
				Context.CurrentContext.SqlParser = new OleDbSqlParser(_sourceDocument.Connection.ConnectionString);
			}
			catch (OleDbException ex)
			{
				throw new CannotConnectToDBException("Unable to open database, verify connection string.", _sourceDocument.Connection.ConnectionString, ex);
			}
			_globalConnection = _sourceDocument.Global.Connection;
			String outputFileName = outputFile;
			try
			{
				_output = new StreamWriter(outputFileName, false, Encoding.GetEncoding(1251));
			}
			catch (UnauthorizedAccessException ex)
			{
				throw new CannotOpenOutputFileException("Ќет доступа к выходному файлу, возможно он защищен от чтени€", ex);
			}
			_output.WriteLine("// SafeQueryGen generated file");
			_output.WriteLine();
			String define = Path.GetFileNameWithoutExtension(outputFileName) + 'H';
			_output.WriteLine("#ifndef {0}", define);
			_output.WriteLine("#define {0}", define);
			_output.WriteLine();
            _output.WriteLine("#include <safequerygen\\misc.h>");
			_output.WriteLine("#include <vcl\\forms.hpp> // using Application variable");
			_output.WriteLine("#include <vcl\\adodb.hpp>");
            _output.WriteLine("#include <vcl\\system.hpp>");
			_output.WriteLine("#include <vcl\\sysutils.hpp>");
			_output.WriteLine("#include <cassert>");
			_output.WriteLine("#include <memory> // using std::auto_ptr");
            _output.WriteLine();
            if (_sourceDocument.References != null && _sourceDocument.References.Length > 0)
            {
                foreach (string include in _sourceDocument.References)
                {
                    _output.WriteLine("#include \"{0}\"", Path.ChangeExtension(include, "h"));
                }
                _output.WriteLine();
            }
			_output.WriteLine();
			_output.WriteLine("#pragma option push -w-8027");
			_output.WriteLine();
			foreach (DataSetBase dataSetBase in _sourceDocument.Elements)
			{
				dataSetBase.Write(this);
				_output.WriteLine();
				//_classes.Add(query.QueryInfo.TypeIdentifier);
			}
			_output.WriteLine();
			/*_output.WriteLine("inline std::vector<System::TMetaClass*> Get{0}Classes()", Path.GetFileNameWithoutExtension(outputFileName));
			_output.WriteLine("{");
			_output.WriteLine("\tstd::vector<System::TMetaClass*> result;");
			foreach (String className in _classes)
			{
				_output.WriteLine("\tresult.push_back(__classid({0}));", className);
			}
			_output.WriteLine("\treturn result;");
			_output.WriteLine("}");
			_output.WriteLine();*/
			_output.WriteLine();
			_output.WriteLine("#pragma option pop");
			_output.WriteLine();
			_output.WriteLine("#endif // {0}", define);
			_output.Close();
		}

		public void WriteDataSet(DataSetInfo dataSet)
		{
			/*
			 * —брасываем флаг Readonly т.к. с этим датасетом могут использоватьс€ комманды
			 * которые возвращают те же самые пол€, но разрешенные дл€ редактировани€
			 */
			foreach (FieldInfo fi in dataSet.FieldsInfos)
			{
				fi.IsReadonly = false;
			}

			String dataSetIdentifier = Utils.Pascal(dataSet.TypeIdentifier);
			_output.WriteLine("class {0} : public {1} {{", dataSetIdentifier,
				BuilderClasses.TCustomADODataSet);
			_output.WriteLine("public:");
			foreach (FieldInfo fieldInfo in dataSet.FieldsInfos)
			{
				CppBuilderFieldInfo cppFieldInfo = ExtractFieldInfo(fieldInfo);
				_output.WriteLine("\t__declspec(property(get={0})) {1}* {2}Field;",
					cppFieldInfo.IdentifierCamel, cppFieldInfo.BuilderFieldType,
					cppFieldInfo.IdentifierPascal);
			}
			_output.WriteLine();
			WriteFieldsDeclarations(ExtractFieldsInfos(dataSet.FieldsInfos));
			_output.WriteLine();
			_output.WriteLine("\t__fastcall {0}(Classes::TComponent* owner = NULL)",
				dataSetIdentifier);
			_output.WriteLine("\t\t: {0}(owner)", BuilderClasses.TCustomADODataSet);
			_output.WriteLine("\t{");
			_output.WriteLine("\t\tInit();");
			_output.WriteLine("\t}");
			_output.WriteLine();
			_output.WriteLine("\tvoid Init();");
			_output.WriteLine();
			_output.WriteLine("\tbool IsEmpty() { return RecordCount == 0; }");
			_output.WriteLine();
			WriteLocates(ExtractFieldsInfos(dataSet.FieldsInfos), BuilderClasses.TCustomADODataSet);
			_output.WriteLine();
			foreach(Command command in dataSet.Commands)
			{
				String[] formalParameters = ExtractFormalParameters(command.Parameters);
				_output.WriteLine("\tvoid {0}({1});",
					command.MethodName, String.Join(", ", formalParameters));
			}
			_output.WriteLine();
			_output.WriteLine("private:");
			if (dataSet.FieldsInfos.Count > 0)
			{
				WriteFieldsFieldsDeclarations(ExtractFieldsInfos(dataSet.FieldsInfos));
				_output.WriteLine();
			}
			if (dataSet.FieldsInfos.Count > 0)
			{
				_output.WriteLine();
                _output.WriteLine("public:");
				WriteFieldsAccessors(ExtractFieldsInfos(dataSet.FieldsInfos));
			}
			_output.WriteLine("};");
			_output.WriteLine();
			foreach(Command command in dataSet.Commands)
			{
				String commandIdentifier = Utils.Pascal(command.Name);
				_output.WriteLine("class {0} : public {1} {{", commandIdentifier,
					BuilderClasses.TADOCommand);
				_output.WriteLine("public:");
				foreach (Parameter parameter in command.Parameters)
				{
					_output.WriteLine("\t__declspec(property(get = _{0}Param)) {1}* {2}Param;",
						Utils.Camel(parameter.Name), BuilderClasses.TParameter,
						Utils.Pascal(parameter.Name));
				}
				_output.WriteLine();
				_output.WriteLine("\t__fastcall {0}(Classes::TComponent* owner = NULL)", commandIdentifier);
				_output.WriteLine("\t\t: {0}(owner)", BuilderClasses.TADOCommand);
				_output.WriteLine("\t{");
				_output.WriteLine("\t\tCommandText =");
				_output.WriteLine(Utils.CStringFromString("\t\t\t", command.NormalizedSql) + ";");
				_output.WriteLine();
				foreach (Parameter parameter in command.Parameters)
				{
					_output.WriteLine("\t\t_{0}Param = Parameters->CreateParameter(L\"{1}\", {2}, {3}, {4}, Variants::Null());",
						Utils.Camel(parameter.Name), parameter.Name,
						Convertions.ParamTypeFromOleDbType(parameter.Type),
						"pdInput", parameter.Size);
				}
				_output.WriteLine();
				_output.WriteLine("\t\tConnection = SafeQueryGen::GetDefaultConnection();");
				_output.WriteLine("\t}");
				_output.WriteLine();
				_output.WriteLine("private:");
				foreach (Parameter parameter in command.Parameters)
				{
					_output.WriteLine("\tAdodb::TParameter* _{0}Param;",
						Utils.Camel(parameter.Name));
				}
				_output.WriteLine("};");
				_output.WriteLine();
				_output.WriteLine("inline void {0}::{1}({2}) {{",
					dataSetIdentifier, command.MethodName, String.Join(", ", ExtractFormalParameters(command.Parameters)));
                _output.WriteLine("\tstd::auto_ptr<{0}> cmd(new {0}(0));", command.Name);
				foreach (Parameter parameter in command.Parameters)
				{
					_output.WriteLine("\tcmd->{1}Param->Value = {2};",
						Utils.Camel(command.Name),
						Utils.Pascal(parameter.Name), Utils.Camel(parameter.Name));
				}
                _output.WriteLine("\tSafeQueryGen::OpenDatasetWithCommand(this, cmd.get());");
				_output.WriteLine("}");
				_output.WriteLine();
			}
			_output.WriteLine("inline void {0}::Init() {{", dataSetIdentifier);
			WriteFieldsInitialisations(1, ExtractFieldsInfos(dataSet.FieldsInfos));
			_output.WriteLine("}");
		}

		public void WriteQuery(QueryInfo queryInfo)
		{
			String[] parametersFieldsDeclarations = new String[queryInfo.Parameters.Count];
			String[] parametersPropertiesDeclarations = new String[queryInfo.Parameters.Count];
			Int32 paramIndex = 0;
			foreach (Parameter parameter in queryInfo.Parameters)
			{
				String parameterFieldName = "_" + Utils.Camel(parameter.Name) + "Param";
				String propertyName = Utils.Pascal(parameter.Name) + "Param";
				String cppBuilderTypeEnum = Convertions.ParamTypeFromOleDbType(parameter.Type);
				//String cppBuilderDirectionEnum = CppBuilder.FromParamDirection(parameter.Direction);
				String cppBuilderType = Convertions.CppTypeFromOleDbType(parameter.Type);

				parametersFieldsDeclarations[paramIndex] = String.Format("{0}* {1};", BuilderClasses.TParameter, parameterFieldName);
				parametersPropertiesDeclarations[paramIndex] = String.Format("__declspec(property(get={0})) {1}* {2};", parameterFieldName, BuilderClasses.TParameter, propertyName);
				paramIndex++;
			}

			if (queryInfo.FieldsInfos.Count > 0)
			{
				WriteRecordset(queryInfo.RecordsetInfo);
			}
			_output.WriteLine();
			_output.WriteLine("class {0} : public {1} {{", queryInfo.TypeIdentifier, BuilderClasses.TADOQuery);
			_output.WriteLine("public:");
			if (queryInfo.FieldsInfos.Count > 0)
			{
				_output.WriteLine("\t__declspec(property(get=Get{0})) {1} {0};", queryInfo.RecordsetInfo.TypeIdentifier, queryInfo.RecordsetInfo.AutoptrTypeIdentifier);
				foreach (FieldInfo fieldInfo in queryInfo.FieldsInfos)
				{
					CppBuilderFieldInfo cppFieldInfo = ExtractFieldInfo(fieldInfo);
					_output.WriteLine("\t__declspec(property(get={0})) {1}* {2}Field;", cppFieldInfo.IdentifierCamel, cppFieldInfo.BuilderFieldType, cppFieldInfo.IdentifierPascal);
				}
				_output.WriteLine();
				WriteFieldsDeclarations(ExtractFieldsInfos(queryInfo.FieldsInfos));
				_output.WriteLine();
			}
			/*if (fieldPropertyDeclarations.Length > 0)
			{
				_output.WriteLine("\t" + String.Join(Environment.NewLine + "\t", fieldPropertyDeclarations));
				_output.WriteLine();
			}*/
			if (parametersPropertiesDeclarations.Length > 0)
			{
				_output.WriteLine("\t" + String.Join(Environment.NewLine + "\t", parametersPropertiesDeclarations));
				_output.WriteLine();
			}
			_output.WriteLine("\t__fastcall {0}(Classes::TComponent* owner = NULL)", queryInfo.TypeIdentifier);
			_output.WriteLine("\t\t: {0}(owner)", BuilderClasses.TADOQuery);
			_output.WriteLine("\t{");
			_output.WriteLine("\t\tInit();");
			_output.WriteLine("\t}");
			_output.WriteLine();
			_output.WriteLine("\tvoid Init() {");
			_output.WriteLine("\t\tSQL->Text = {0};", Utils.CStringFromString("\t\t\t", queryInfo.SqlText));
			if (queryInfo.CalcFields.Count > 0)
			{
				_output.WriteLine("\t\tOnCalcFields = this->OnCalcFieldsHandler;");
			}
			if (queryInfo.FieldsInfos.Count > 0)
			{
				WriteFieldsInitialisations(2, ExtractFieldsInfos(queryInfo.FieldsInfos));
			}
			if (queryInfo.Parameters.Count > 0)
			{
				_output.WriteLine();
				WriteParametersInitialisations(queryInfo.Parameters);
			}
			_output.WriteLine("\t\tConnection = SafeQueryGen::GetDefaultConnection();");
			/*if (_globalConnection != null)
			{
				_output.WriteLine();
				_output.WriteLine("\t\tConnection = {0};", _globalConnection);
			}*/
			_output.WriteLine("\t}");
			_output.WriteLine();

			if (queryInfo.FieldsInfos.Count > 0)
			{
				WriteIsEmptyMethod();
				_output.WriteLine();
				WriteFieldByNameMethod();
				_output.WriteLine();
				WriteOpenMethods(queryInfo.Parameters, BuilderClasses.TADOQuery);
				_output.WriteLine();
				WriteCloneMethods(queryInfo.TypeIdentifier, BuilderClasses.TADOQuery);
				_output.WriteLine();
				WriteLocates(ExtractFieldsInfos(queryInfo.FieldsInfos), BuilderClasses.TADOQuery);
				_output.WriteLine();
			}
			else
			{
				WriteExecuteMethod(queryInfo.TypeIdentifier, queryInfo.Parameters);
				_output.WriteLine();
			}

			if (queryInfo.FieldsInfos.Count > 0 && queryInfo.Parameters.Count == 0)
			{
				WriteGetForLookupMethod(queryInfo.TypeIdentifier);
				_output.WriteLine();
			}


			// статический метод дл€ открыти€ запроса
			//
			if (_globalConnection != null)
			{
				if (queryInfo.FieldsInfos.Count > 0)
				{
					WriteStaticOpenMethods(queryInfo.TypeIdentifier, queryInfo.Parameters);
					_output.WriteLine();
				}
				else
				{
					WriteStaticExecuteMethod(queryInfo.TypeIdentifier, queryInfo.Parameters);
					_output.WriteLine();
				}
			}

			_output.WriteLine("private:");
			_output.WriteLine("\t// Disable unsafe properties");
			_output.WriteLine("\t__property SQL;");
			_output.WriteLine("\t__property Parameters;");
			_output.WriteLine();
			if (queryInfo.CalcFields.Count > 0)
			{
				_output.WriteLine("\tvoid __fastcall OnCalcFieldsHandler(Db::TDataSet* sender) { assert(this == sender); DoCalcFields(); }");
				_output.WriteLine("\tvoid DoCalcFields(); // Must be implemented by consumer");
			}
			if (queryInfo.FieldsInfos.Count > 0)
			{
				_output.WriteLine("\t{0} Get{1}() {{ return ({0})Recordset; }}", queryInfo.RecordsetInfo.AutoptrTypeIdentifier, queryInfo.TypeIdentifier + "Recordset");
				WriteFieldsFieldsDeclarations(ExtractFieldsInfos(queryInfo.FieldsInfos));
				_output.WriteLine();
                _output.WriteLine("public:");
				WriteFieldsAccessors(ExtractFieldsInfos(queryInfo.FieldsInfos));
				_output.WriteLine();

                _output.WriteLine("private:");
				_output.WriteLine("\tvoid ExecSQL(); // block from using");
			}
			else
			{
				_output.WriteLine("\tvoid Open(); // block from using");
			}
			_output.WriteLine();
			if (parametersFieldsDeclarations.Length > 0)
			{
				_output.WriteLine("\t" + String.Join(Environment.NewLine + "\t", parametersFieldsDeclarations));
			}

			_output.WriteLine("};");
			if (queryInfo.FieldsInfos.Count > 0)
			{
				_output.WriteLine();
				WriteAutoPtrDataSet(queryInfo.TypeIdentifier, queryInfo.Parameters);
			}
		}

		public void WriteTable(TableInfo tableInfo)
		{
			WriteRecordset(tableInfo.RecordsetInfo);
			_output.WriteLine();
			_output.WriteLine("class {0} : public {1} {{", tableInfo.TypeIdentifier, BuilderClasses.TADOTable);
			_output.WriteLine("public:");
			_output.WriteLine("\t__declspec(property(get=Get{0})) {1} {0};", tableInfo.TypeIdentifier + "Recordset", tableInfo.RecordsetInfo.AutoptrTypeIdentifier);
			WriteFieldsDeclarations(ExtractFieldsInfos(tableInfo.FieldsInfos));
			_output.WriteLine();
			_output.WriteLine("\t__fastcall {0}(Classes::TComponent* owner = NULL)", tableInfo.TypeIdentifier);
			_output.WriteLine("\t\t: {0}(owner)", BuilderClasses.TADOTable);
			_output.WriteLine("\t{");
			_output.WriteLine("\t\tTableDirect = true;");
			_output.WriteLine("\t\tTableName = \"{0}\";", tableInfo.TableName);
			WriteFieldsInitialisations(2, ExtractFieldsInfos(tableInfo.FieldsInfos));
			_output.WriteLine("\t\tConnection = SafeQueryGen::GetDefaultConnection();");
			/*if (_globalConnection != null)
			{
				_output.WriteLine();
				_output.WriteLine("\t\tConnection = {0};", _globalConnection);
			}*/
			_output.WriteLine("\t}");
			_output.WriteLine();
			WriteOpenMethods(null, BuilderClasses.TADOTable);
			_output.WriteLine();
			WriteCloneMethods(tableInfo.TypeIdentifier, BuilderClasses.TADOTable);
			_output.WriteLine();
			WriteLocates(ExtractFieldsInfos(tableInfo.FieldsInfos), BuilderClasses.TADOTable);
			_output.WriteLine();
			WriteGetForLookupMethod(tableInfo.TypeIdentifier);
			_output.WriteLine();
			_output.WriteLine("private:");
			_output.WriteLine("\t{0} Get{1}() {{ return ({0})Recordset; }}", tableInfo.RecordsetInfo.AutoptrTypeIdentifier, tableInfo.TypeIdentifier + "Recordset");
			WriteFieldsFieldsDeclarations(ExtractFieldsInfos(tableInfo.FieldsInfos));
			_output.WriteLine();
            _output.WriteLine("public:");
			WriteFieldsAccessors(ExtractFieldsInfos(tableInfo.FieldsInfos));
			_output.WriteLine("};");
		}

		void WriteRecordset(RecordsetInfo rsti)
		{
			_output.WriteLine("__interface {0}; // forward declaration", rsti.TypeIdentifier);
			_output.WriteLine("typedef System::DelphiInterface<{0}> {1};", rsti.TypeIdentifier, rsti.AutoptrTypeIdentifier);
			_output.WriteLine();
			_output.WriteLine("__interface INTERFACE_UUID(\"{{00000555-0000-0010-8000-00AA006D2EA4}}\") {0} : public Adoint::_Recordset", rsti.TypeIdentifier);
			_output.WriteLine("{");
			_output.WriteLine("public:");
			foreach (FieldInfo fi in rsti.FieldsInfos)
			{
				CppBuilderFieldInfo cppFi = ExtractFieldInfo(fi);
				String parameterType = Convertions.DecorateParameterType(cppFi.CppType);
				_output.WriteLine("\t__declspec(property(get=Get{0}, put=Put{0})) {1} {0};", cppFi.IdentifierPascal, cppFi.CppType);
				_output.WriteLine("\t__declspec(property(get=GetOld{0})) {1} Old{0};", cppFi.IdentifierPascal, cppFi.CppType);
				_output.WriteLine("\tbool Is{0}Null() {{ return Fields->Item[{1}]->Value.IsNull(); }}", cppFi.IdentifierPascal, cppFi.Ordinal);
				_output.WriteLine("\tbool IsOld{0}Null() {{ return Fields->Item[{1}]->OriginalValue.IsNull(); }}", cppFi.IdentifierPascal, cppFi.Ordinal);
				_output.WriteLine("\t{0} Nz{2}({1} valueIfNull) {{ if(Is{2}Null()) return valueIfNull; else return {2}; }};", cppFi.CppType, parameterType, cppFi.IdentifierPascal);
			}
			_output.WriteLine();
			_output.WriteLine("\t{0} Clone(int lockType) {{", rsti.AutoptrTypeIdentifier);
			_output.WriteLine("\t\tAdoint::_di__Recordset result;");
			_output.WriteLine("\t\t((Adoint::_Recordset*)this)->Clone(lockType, result);");
			_output.WriteLine("\t\treturn ({0})result;", rsti.AutoptrTypeIdentifier);
			_output.WriteLine("\t}");
			_output.WriteLine();
			_output.WriteLine("public:");
			foreach (FieldInfo fi in rsti.FieldsInfos)
			{
				CppBuilderFieldInfo cppFi = ExtractFieldInfo(fi);
				String parameterType = Convertions.DecorateParameterType(cppFi.CppType);
				_output.WriteLine("\t{0} Get{1}() {{ return Fields->Item[{2}]->Value; }}", cppFi.CppType, cppFi.IdentifierPascal, cppFi.Ordinal);
				_output.WriteLine("\tvoid Put{0}({1} value) {{ Fields->Item[{2}]->Value = value; }}", cppFi.IdentifierPascal, parameterType, cppFi.Ordinal);
                _output.WriteLine("\t{0} GetOld{1}() {{ return Fields->Item[{2}]->OriginalValue.operator {0}(); }}", cppFi.CppType, cppFi.IdentifierPascal, cppFi.Ordinal);
			}
			_output.WriteLine("};");
		}
	}
}
