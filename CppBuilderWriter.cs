using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SafeQueryGen
{
	public class CppBuilderFieldInfo : FieldInfo, ICloneable
	{
		public CppBuilderFieldInfo(FieldInfo src)
			: base(src)
		{
		}

		public String CppType;
		public String BuilderFieldType;
		public String IdentifierPascal;
		public String IdentifierCamel;

		public new CppBuilderFieldInfo Clone() { return (CppBuilderFieldInfo)MemberwiseClone(); }

		#region ICloneable Members

		object ICloneable.Clone() { return MemberwiseClone(); }

		#endregion
	}

	public class CppBuilderWriter
	{
		protected StreamWriter _output;

		protected List<CppBuilderFieldInfo> ExtractFieldsInfos(ICollection<FieldInfo> fieldsInfos)
		{
			List<CppBuilderFieldInfo> result = new List<CppBuilderFieldInfo>();
			foreach (FieldInfo fieldInfo in fieldsInfos)
			{
				result.Add(ExtractFieldInfo(fieldInfo));
			}
			return result;
		}

		protected CppBuilderFieldInfo ExtractFieldInfo(FieldInfo fieldInfo)
		{
			CppBuilderFieldInfo cppFieldInfo = new CppBuilderFieldInfo(fieldInfo);
			BuilderFieldType builderType = Convertions.FromFieldType(fieldInfo.FieldType);
			cppFieldInfo.BuilderFieldType = builderType.FieldTypeName;
			cppFieldInfo.CppType = builderType.CppTypeName;
			String identifier = fieldInfo.RenamedName != null ? fieldInfo.RenamedName : fieldInfo.FieldName;
			cppFieldInfo.IdentifierPascal = Utils.Pascal(identifier);
			cppFieldInfo.IdentifierCamel = Utils.Camel(identifier);
			return cppFieldInfo;
		}

		protected String[] ExtractFormalParameters(List<Parameter> parameters)
		{
			String[] formalParameters = new String[parameters.Count];
			Int32 paramIndex = 0;
			foreach (Parameter parameter in parameters)
			{
				formalParameters[paramIndex] =
					Convertions.CppTypeFromOleDbType(parameter.Type) + ' ' +
					Utils.Camel(parameter.Name);
				paramIndex++;
			}
			return formalParameters;
		}

		protected void WriteFieldsInitialisations(Int32 ident, ICollection<CppBuilderFieldInfo> fieldsInfos)
		{
			foreach (CppBuilderFieldInfo fieldInfo in fieldsInfos)
			{
				WriteFieldInitialisation(ident, fieldInfo);
			}
		}

		protected void WriteFieldInitialisation(Int32 ident, CppBuilderFieldInfo fieldInfo)
		{
			String identStr = new String('\t', ident);
			_output.WriteLine("{0}{1} = new {2}(this);", identStr, fieldInfo.IdentifierCamel, fieldInfo.BuilderFieldType);
			_output.WriteLine("{0}{1}->FieldName = \"{2}\";", identStr, fieldInfo.IdentifierCamel, fieldInfo.FieldName);
			if (fieldInfo.BuilderFieldType == BuilderClasses.TStringField ||
				fieldInfo.BuilderFieldType == BuilderClasses.TWideStringField)
			{
				_output.WriteLine("{0}{1}->Size = {2};", identStr, fieldInfo.IdentifierCamel, fieldInfo.Size);
			}
			if (fieldInfo.BuilderFieldType == BuilderClasses.TBCDField)
			{
				if (fieldInfo.Precision == 0)
				{
					fieldInfo.Precision = 19;
				}
				_output.WriteLine("{0}{1}->Precision = {2};", identStr, fieldInfo.IdentifierCamel, fieldInfo.Precision);
			}
			if (fieldInfo.BuilderFieldType == BuilderClasses.TMemoField)
			{
				_output.WriteLine("{0}{1}->BlobType = ftMemo;", identStr, fieldInfo.IdentifierCamel);
			}
			if (fieldInfo.IsAutoIncrement)
			{
				_output.WriteLine("{0}{1}->AutoGenerateValue = arAutoInc;", identStr, fieldInfo.IdentifierCamel);
			}
			if (fieldInfo.Lookup != null)
			{
				_output.WriteLine("{0}{1}->FieldKind = fkLookup;", identStr, fieldInfo.IdentifierCamel);
				_output.WriteLine("{0}{1}->Lookup = true;", identStr, fieldInfo.IdentifierCamel);
				_output.WriteLine("{0}{1}->KeyFields = \"{2}\";", identStr, fieldInfo.IdentifierCamel, fieldInfo.Lookup.LocalKeyFields);
				_output.WriteLine("{0}{1}->LookupKeyFields = \"{2}\";", identStr, fieldInfo.IdentifierCamel, fieldInfo.Lookup.LookupKeyFields);
				_output.WriteLine("{0}{1}->LookupResultField = \"{2}\";", identStr, fieldInfo.IdentifierCamel, fieldInfo.Lookup.LookupResultField);
				if (fieldInfo.Lookup.DataSet != null)
				{
					_output.WriteLine("{0}{1}->LookupDataSet = {2}::GetForLookup();", identStr, fieldInfo.IdentifierCamel, fieldInfo.Lookup.DataSet);
					//_output.WriteLine("{0}InsertComponent({1}->LookupDataSet);", identStr, fieldInfo.IdentifierCamel);
				}
			}
			else if (fieldInfo.CalcField != null)
			{
				_output.WriteLine("{0}{1}->FieldKind = fkCalculated;", identStr, fieldInfo.IdentifierCamel);
				_output.WriteLine("{0}{1}->Calculated = true;", identStr, fieldInfo.IdentifierCamel);
			}
			_output.WriteLine("{0}{1}->DataSet = this;", identStr, fieldInfo.IdentifierCamel);
		}

		protected void WriteFieldsDeclarations(ICollection<CppBuilderFieldInfo> fieldsInfos)
		{
			foreach (CppBuilderFieldInfo fieldInfo in fieldsInfos)
			{
				String parameterType = Convertions.DecorateParameterType(fieldInfo.CppType);
				_output.WriteLine("\t// IsReadonly = {0}  IsUnique = {1}  IsKey = {2}  BaseColumnName = {3}",
					fieldInfo.IsReadonly, fieldInfo.IsUnique, fieldInfo.IsKey, fieldInfo.BaseColumnName);
				_output.WriteLine("\t__declspec(property(get=Get{0}, put=Put{0})) {1} {0};", fieldInfo.IdentifierPascal, fieldInfo.CppType);
				if (fieldInfo.IsNullable)
				{
					_output.WriteLine("\tvoid Set{0}Null() {{ this->{1}->Clear(); }}", fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
				}
				_output.WriteLine("\t__declspec(property(get=GetOld{0})) {1} Old{0};", fieldInfo.IdentifierPascal, fieldInfo.CppType);
				_output.WriteLine("\tbool IsOld{0}Null() {{ return this->{1}->OldValue.IsNull(); }}", fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
				_output.WriteLine("\t__declspec(property(get=GetNew{0})) {1} New{0};", fieldInfo.IdentifierPascal, fieldInfo.CppType);
				_output.WriteLine("\tbool IsNew{0}Null() {{ return this->{1}->NewValue.IsNull(); }}", fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
				_output.WriteLine("\tbool Is{0}Null() {{ return this->{1}->IsNull; }}", fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
                _output.WriteLine("\t{0} Nz{2}({1} valueIfNull) {{ if(Is{2}Null()) return valueIfNull; else return {2}; }}", fieldInfo.CppType, parameterType, fieldInfo.IdentifierPascal);
                _output.WriteLine("\t{0} NzOld{2}({1} valueIfNull) {{ if(IsOld{2}Null()) return valueIfNull; else return {2}; }}", fieldInfo.CppType, parameterType, fieldInfo.IdentifierPascal);
                _output.WriteLine("\t{0} NzNew{2}({1} valueIfNull) {{ if(IsNew{2}Null()) return valueIfNull; else return {2}; }}", fieldInfo.CppType, parameterType, fieldInfo.IdentifierPascal);
			}
		}

		protected void WriteFieldsAccessors(ICollection<CppBuilderFieldInfo> fieldsInfos)
		{
			foreach (CppBuilderFieldInfo fieldInfo in fieldsInfos)
			{
				String parameterType = Convertions.DecorateParameterType(fieldInfo.CppType);
				_output.WriteLine("\t{0} Get{1}() {{ return this->{2}->Value; }}", fieldInfo.CppType, fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
				_output.WriteLine("\tvoid Put{0}({1} value) {{ this->{2}->Value = value; }}", fieldInfo.IdentifierPascal, parameterType, fieldInfo.IdentifierCamel);
                _output.WriteLine("\t{0} GetOld{1}() {{ return this->{2}->OldValue.operator {0}(); }}", fieldInfo.CppType, fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
                _output.WriteLine("\t{0} GetNew{1}() {{ return this->{2}->NewValue.operator {0}(); }}", fieldInfo.CppType, fieldInfo.IdentifierPascal, fieldInfo.IdentifierCamel);
			}
		}

		protected static CppBuilderFieldInfo[] GetKeyFields(ICollection<CppBuilderFieldInfo> fieldsInfos)
		{
			List<CppBuilderFieldInfo> keyFields = new List<CppBuilderFieldInfo>();
			foreach (CppBuilderFieldInfo fi in fieldsInfos)
			{
				if (fi.IsKey)
					keyFields.Add(fi);
			}
			return keyFields.ToArray();
		}

		protected void WriteLocates(ICollection<CppBuilderFieldInfo> fieldInfos, String baseClass)
		{
			CppBuilderFieldInfo[] keyFields = GetKeyFields(fieldInfos);
			if (keyFields.Length > 1)
				WriteLocateBy(keyFields, baseClass);
			foreach (CppBuilderFieldInfo fi in fieldInfos)
			{
				WriteLocateBy(new CppBuilderFieldInfo[] { fi }, baseClass);
			}
		}

		protected void WriteLocateBy(IList<CppBuilderFieldInfo> fieldInfos, String baseClass)
		{
			if (fieldInfos.Count == 0)
				return;
			List<String> fieldNamesPascal = new List<String>(fieldInfos.Count);
			List<String> fieldNames = new List<String>(fieldInfos.Count);
			List<String> parameters = new List<String>(fieldInfos.Count);
			List<String> namesCamel = new List<String>(fieldInfos.Count);
			foreach (CppBuilderFieldInfo fi in fieldInfos)
			{
				fieldNames.Add(fi.FieldName);
				fieldNamesPascal.Add(fi.FieldName);
				String parameterType = Convertions.DecorateParameterType(fi.CppType);
				parameters.Add(parameterType + " " + fi.IdentifierCamel);
				namesCamel.Add(fi.IdentifierCamel);
			}
			_output.WriteLine("\tbool LocateBy{0}({1}, Db::TLocateOptions locateOptions = Db::TLocateOptions()) {{",
				String.Join("", fieldNamesPascal.ToArray()),
				String.Join(", ", parameters.ToArray()));
			if (fieldInfos.Count > 1)
			{
				_output.WriteLine("\t\tVariant values[] = {{ {0} }};", String.Join(", ", namesCamel.ToArray()));
				_output.WriteLine("\t\treturn {0}::Locate(\"{1}\", VarArrayOf(values, {2}), locateOptions);",
					baseClass, String.Join(";", fieldNames.ToArray()), fieldNames.Count - 1);
			}
			else
			{
				_output.WriteLine("\t\treturn {0}::Locate(\"{1}\", {2}, locateOptions);",
					baseClass, fieldNames[0], namesCamel[0]);
			}
			_output.WriteLine("\t}");
			_output.WriteLine();
			/*_output.WriteLine("\tbool LocateBy(std::vector<TField> fields, std::vector<Variant> values, Db::TLocateOptions locateOptions = Db::TLocateOptions()) {");
			_output.WriteLine("\t\tassert(fields.size() == values.size());");
			_output.WriteLine("\t\tstd::string fieldsString;");
			_output.WriteLine("\t\tfor(int i = 0; i < fields.size(); i++) {");
			_output.WriteLine("\t\t\tfieldsString += ");
			_output.WriteLine("\t\t}");
			_output.WriteLine("\t\treturn {0}::Locate();");
			_output.WriteLine("\t}");*/
		}

		protected void WriteFieldsFieldsDeclarations(ICollection<CppBuilderFieldInfo> fieldsInfos)
		{
			foreach (CppBuilderFieldInfo fieldInfo in fieldsInfos)
			{
				_output.WriteLine("\t{0}* {1};", fieldInfo.BuilderFieldType, fieldInfo.IdentifierCamel);
			}
		}

		protected void WriteParametersInitialisations(IEnumerable<Parameter> parameters)
		{
			List<String> parametersInitialisations = new List<String>();
			foreach (Parameter parameter in parameters)
			{
				String parameterFieldName = "_" + Utils.Camel(parameter.Name) + "Param";
				String cppBuilderTypeEnum = Convertions.ParamTypeFromOleDbType(parameter.Type);
				//String cppBuilderDirectionEnum = CppBuilder.FromParamDirection(parameter.Direction);
				parametersInitialisations.Add(
					String.Format("{0} = Parameters->CreateParameter(L\"{1}\", {2}, {3}, {4}, Variants::Null());",
					parameterFieldName, parameter.Name, cppBuilderTypeEnum, "pdInput", parameter.Size));
			}
			_output.WriteLine("\t\t" + String.Join(Environment.NewLine + "\t\t", parametersInitialisations.ToArray()));
		}

		protected void WriteFieldByNameMethod()
		{
			_output.WriteLine("\tvoid FieldByName(); // block programmers from using unsafe FieldByName");
		}

		protected void WriteIsEmptyMethod()
		{
			_output.WriteLine("\tbool IsEmpty() { return RecordCount == 0 && State != dsInsert; }");
		}

		protected void WriteCloneMethods(String typeIdentifier, String baseClass)
		{
			_output.WriteLine("\t{0}* Clone(Adodb::TADOLockType lockType, Classes::TComponent* owner = NULL) {{", typeIdentifier);
			_output.WriteLine("\t\tstd::auto_ptr<{0}> result(new {0}(owner));", typeIdentifier);
			_output.WriteLine("\t\t(({0}*)result.get())->Clone(this, lockType);", baseClass);
			_output.WriteLine("\t\treturn result.release();");
			_output.WriteLine("\t}");
			_output.WriteLine();

			_output.WriteLine("\t{0}* Clone(Classes::TComponent* owner = NULL) {{", typeIdentifier);
			_output.WriteLine("\t\treturn Clone(this->LockType, owner);");
			_output.WriteLine("\t}");
			_output.WriteLine();
		}

		struct ParametersInfo
		{
			public String[] MethodParamsDeclaration;
			public String[] MethodParamsVariables;
			public String[] ParametersSetting;

			public static ParametersInfo Extract(ICollection<Parameter> parameters)
			{
				ParametersInfo result = new ParametersInfo();
				if (parameters == null)
				{
					result.MethodParamsDeclaration = new String[0];
					result.MethodParamsVariables = new String[0];
					result.ParametersSetting = new String[0];
					return result;
				}
				result.MethodParamsDeclaration = new String[parameters.Count];
				result.MethodParamsVariables = new String[parameters.Count];
				result.ParametersSetting = new String[parameters.Count];
				Int32 index = 0;
				foreach (Parameter parameter in parameters)
				{
					String fieldName = "_" + Utils.Camel(parameter.Name) + "Param";
					String variableName = Utils.Camel(parameter.Name);
					String cppType = Convertions.CppTypeFromOleDbType(parameter.Type);
					String parameterType = Convertions.DecorateParameterType(cppType);
					result.MethodParamsDeclaration[index] = (String.Format("{0} {1}", parameterType, variableName));
					result.MethodParamsVariables[index] = (variableName);
					result.ParametersSetting[index] = (String.Format("{0}->Value = {1}", fieldName, variableName));
					index++;
				}
				return result;
			}
		}

		protected void WriteOpenMethods(ICollection<Parameter> parameters, String baseClass)
		{
			ParametersInfo paramsInfo = ParametersInfo.Extract(parameters);

			Int32 parametersCount;
			if (parameters == null)
				parametersCount = 0;
			else
				parametersCount = parameters.Count;

			_output.WriteLine("\tvoid Open({0}) {{", String.Join(", ", paramsInfo.MethodParamsDeclaration));
			_output.WriteLine("\t\t{0};", String.Join(";" + Environment.NewLine + "\t\t", paramsInfo.ParametersSetting));
			_output.WriteLine("\t\tJET_BUG_WRAPPING_BEGIN");
			_output.WriteLine("\t\t{0}::Open();", baseClass);
			_output.WriteLine("\t\tJET_BUG_WRAPPING_END");
			_output.WriteLine("\t}");
			_output.WriteLine();

			String[] methodParameters = new String[parametersCount + 1];
			methodParameters[0] = "Adodb::TADOConnection* connection";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 1);
			_output.WriteLine("\tvoid Open({0}) {{", String.Join(", ", methodParameters));
			_output.WriteLine("\t\tConnection = connection;");
			_output.WriteLine("\t\tOpen({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine();

			methodParameters = new String[parametersCount + 2];
			methodParameters[0] = "Adodb::TADOConnection* connection";
			methodParameters[1] = "Adodb::TADOLockType lockType";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 2);
			_output.WriteLine("\tvoid Open({0}) {{", String.Join(", ", methodParameters));
			_output.WriteLine("\t\tConnection = connection;");
			_output.WriteLine("\t\tLockType = SafeQueryGen::_LockTypeShutWarn(lockType); // shut warting 8006 (bei)");
			_output.WriteLine("\t\tOpen({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine();

			methodParameters = new String[parametersCount + 2];
			methodParameters[0] = "Adodb::TADOLockType lockType";
			methodParameters[1] = "Adodb::TCursorType cursorType";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 2);
			_output.WriteLine("\tvoid Open({0}) {{", String.Join(", ", methodParameters));
			_output.WriteLine("\t\tLockType = SafeQueryGen::_LockTypeShutWarn(lockType); // shut warting 8006 (bei)");
			_output.WriteLine("\t\tCursorType = SafeQueryGen::_CursorTypeShutWarn(cursorType); // shut warting 8006 (bei)");
			_output.WriteLine("\t\tOpen({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine();

			methodParameters = new String[parametersCount + 1];
			methodParameters[0] = "Adodb::TADOLockType lockType";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 1);
			_output.WriteLine("\tvoid Open({0}) {{", String.Join(", ", methodParameters));
			_output.WriteLine("\t\tLockType = SafeQueryGen::_LockTypeShutWarn(lockType); // shut warting 8006 (bei)");
			_output.WriteLine("\t\tOpen({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine();
		}

		protected void WriteStaticOpenMethods(String typeIdentifier, ICollection<Parameter> parameters)
		{
			ParametersInfo paramsInfo = ParametersInfo.Extract(parameters);

			_output.WriteLine("\tstatic {0}* StaticOpen({1}) {{", typeIdentifier, String.Join(", ", paramsInfo.MethodParamsDeclaration));
			_output.WriteLine("\t\tstd::auto_ptr<{0}> result(new {0});", typeIdentifier);
			_output.WriteLine("\t\tresult->Open({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t\treturn result.release();");
			_output.WriteLine("\t}");
			_output.WriteLine();

			String[] methodParameters = new String[paramsInfo.MethodParamsDeclaration.Length + 1];
			methodParameters[0] = "Adodb::TADOLockType lockType";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 1);
			_output.WriteLine("\tstatic {0}* StaticOpen({1}) {{", typeIdentifier, String.Join(", ", methodParameters));
			_output.WriteLine("\t\tstd::auto_ptr<{0}> result(new {0});", typeIdentifier);
			methodParameters = new String[paramsInfo.MethodParamsDeclaration.Length + 1];
			methodParameters[0] = "lockType";
			paramsInfo.MethodParamsVariables.CopyTo(methodParameters, 1);
			_output.WriteLine("\t\tresult->Open({0});", String.Join(", ", methodParameters));
			_output.WriteLine("\t\treturn result.release();");
			_output.WriteLine("\t}");
		}

		protected void WriteExecuteMethod(String typeIdentifier, ICollection<Parameter> parameters)
		{
			ParametersInfo paramsInfo = ParametersInfo.Extract(parameters);

			_output.WriteLine("\tint IExecute({0}) {{", String.Join(", ", paramsInfo.MethodParamsDeclaration));
			_output.WriteLine("\t\t{0};", String.Join(";" + Environment.NewLine + "\t\t", paramsInfo.ParametersSetting));
			_output.WriteLine("\t\treturn ExecSQL();");
			_output.WriteLine("\t}");
		}

		protected void WriteStaticExecuteMethod(String typeIdentifier, ICollection<Parameter> parameters)
		{
			ParametersInfo paramsInfo = ParametersInfo.Extract(parameters);

			_output.WriteLine("\tstatic int Execute({0}) {{", String.Join(", ", paramsInfo.MethodParamsDeclaration));
			_output.WriteLine("\t\tstd::auto_ptr<{0}> result(new {0});", typeIdentifier);
			_output.WriteLine("\t\treturn result->IExecute({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
		}

		protected void WriteAutoPtrDataSet(String typeIdentifier, ICollection<Parameter> parameters)
		{
			ParametersInfo paramsInfo = ParametersInfo.Extract(parameters);

			_output.WriteLine("class {0}AutoPtr : public std::auto_ptr<{0}> {{", typeIdentifier);
			_output.WriteLine("public:");
			_output.WriteLine("\t{0}AutoPtr({0}* ptr)", typeIdentifier);
			_output.WriteLine("\t\t: std::auto_ptr<{0}>(ptr)", typeIdentifier);
			_output.WriteLine("\t{");
			_output.WriteLine("\t}");
			_output.WriteLine("\t{0}AutoPtr({1})", typeIdentifier, String.Join(", ", paramsInfo.MethodParamsDeclaration));
			_output.WriteLine("\t\t: std::auto_ptr<{0}>(new {0})", typeIdentifier);
			_output.WriteLine("\t{");
			_output.WriteLine("\t\tstd::auto_ptr<{0}>::get()->Open({1});", typeIdentifier, String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine();

			String[] methodParameters = new String[parameters.Count + 1];
			methodParameters[0] = "Adodb::TADOLockType lockType";
			paramsInfo.MethodParamsDeclaration.CopyTo(methodParameters, 1);
			_output.WriteLine("\t{0}AutoPtr({1})", typeIdentifier, String.Join(", ", methodParameters));
			_output.WriteLine("\t\t: std::auto_ptr<{0}>(new {0})", typeIdentifier);
			_output.WriteLine("\t{");
			_output.WriteLine("\t\t{0} * ptr = std::auto_ptr<{0}>::get();", typeIdentifier);
			_output.WriteLine("\t\tptr->LockType = SafeQueryGen::_LockTypeShutWarn(lockType);");
			_output.WriteLine("\t\tptr->Open({0});", String.Join(", ", paramsInfo.MethodParamsVariables));
			_output.WriteLine("\t}");
			_output.WriteLine("};");
		}

		protected void WriteGetForLookupMethod(String typeIdentifier)
		{
			EmitLn(string.Format("\tstatic Adodb::TCustomADODataSet * GetForLookup()", typeIdentifier));
			EmitLn("\t{");
			EmitLn(string.Format("\t\tstatic Adodb::TCustomADODataSet * lookupInstance(0);", typeIdentifier));
			EmitLn("\t\tif(lookupInstance == 0)");
			EmitLn("\t\t{");
			EmitLn(string.Format("\t\t\tlookupInstance = new {0}(0);", typeIdentifier));
			EmitLn("\t\t\tSafeQueryGen::_RegisterLookup(&lookupInstance);");
			EmitLn("\t\t}");
			EmitLn("\t\tif(lookupInstance->State == dsInactive)");
			EmitLn("\t\t{");
			EmitLn("\t\t\tlookupInstance->LockType = ltReadOnly;");
			EmitLn("\t\t\tlookupInstance->CursorType = ctStatic;");
			EmitLn("\t\t\tlookupInstance->Open();");
			EmitLn("\t\t}");
			EmitLn("\t\treturn lookupInstance;");
			EmitLn("\t}");
		}

		private void Emit(string code)
		{
			_output.Write(code);
		}

		private void EmitLn(string code)
		{
			_output.WriteLine(code);
		}
	}
}
