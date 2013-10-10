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
using System.Xml.Serialization;

namespace SafeQueryGen
{
	public class Utils
	{
		public static String ChopComments(String sql)
		{
			String[] separators = { Environment.NewLine, "\n" };
			String[] lines = sql.Split(separators, StringSplitOptions.None);
			List<String> outputLines = new List<string>();
			Boolean inMultilineComments = false;
			String beginPiece = "";
			foreach (String line in lines)
			{
				String resultLine = line;
				if (!inMultilineComments)
				{
					Int32 commentPos = line.IndexOf("--");
					if (commentPos != -1)
					{
						resultLine = line.Remove(commentPos);
					}
					Int32 commentsBegin = resultLine.IndexOf("/*");
					if (commentsBegin != -1)
					{
						inMultilineComments = true;
						beginPiece = resultLine.Remove(commentsBegin);
					}
				}
				if (inMultilineComments)
				{
					Int32 commentsEnd = resultLine.IndexOf("*/");
					if (commentsEnd != -1)
					{
						inMultilineComments = false;
						resultLine = beginPiece + " " + resultLine.Remove(0, commentsEnd + 2);
					}
				}
				if (!inMultilineComments)
				{
					outputLines.Add(resultLine);
				}
			}
			return String.Join(Environment.NewLine, outputLines.ToArray());
		}

		public static String CStringFromString(String ident, String str)
		{
			Char[] separators = { Environment.NewLine[0], Environment.NewLine[1] };
			String[] lines = str.Split(separators);
			Regex quotes_re = new Regex(@"");
			String[] outputLines = new String[lines.Length];
			Int32 lineIndex = 0;
			foreach (String line in lines)
			{
				if (line == String.Empty)
					continue;
				String goodLine = line;
				goodLine = goodLine.Replace("\\", "\\\\");
				goodLine = goodLine.Replace("\"", "\\\"");
				outputLines[lineIndex] = String.Format("{0}\"{1}\\r\\n\"", ident, goodLine);
				lineIndex++;
			}
			return String.Join(Environment.NewLine, outputLines, 0, lineIndex);
		}

		public static String Camel(String identifier)
		{
			return Char.ToLower(identifier[0]) + identifier.Substring(1);
		}

		public static String Pascal(String identifier)
		{
			return Char.ToUpper(identifier[0]) + identifier.Substring(1);
		}
	}

	public abstract class DataSetBase
	{
		[XmlAttribute("name")]
		public String Name;
		[XmlElement("rename")]
		public Rename[] Renames;
		[XmlElement("lookup")]
		public List<Lookup> Lookups;
		[XmlElement("calcField")]
		public List<CalcField> CalcFields;

		abstract public void Write(Generator generator);
		abstract public DataSetInfoBase Info { get; }
	}

	public struct RecordsetInfo
	{
		public String TypeIdentifier;
		public String AutoptrTypeIdentifier;
		public ICollection<FieldInfo> FieldsInfos;
	}

	public class DataSetInfoBase
	{
		public String TypeIdentifier;
		public ICollection<Rename> Renames;
		public ICollection<FieldInfo> FieldsInfos;
		public ICollection<Lookup> Lookups;
		public ICollection<CalcField> CalcFields;
		public RecordsetInfo RecordsetInfo;

		public FieldInfo FindFieldByName(String fieldName)
		{
			foreach (FieldInfo fieldInfo in FieldsInfos)
			{
				if (fieldInfo.FieldName == fieldName)
				{
					return fieldInfo;
				}
			}
			return null;
		}

		public void Fill(DataSetBase dataSetBase, ParserInfo parserInfo)
		{
			TypeIdentifier = dataSetBase.Name;
			Renames = dataSetBase.Renames;
			FieldsInfos = ExtractFieldsInfos(parserInfo.FieldsInfos, Renames);
			Lookups = dataSetBase.Lookups;
			CalcFields = dataSetBase.CalcFields;
			MegreFieldsWithLookups(FieldsInfos, dataSetBase.Lookups);
			MegreFieldsWithCalcFields(FieldsInfos, dataSetBase.CalcFields);
			RecordsetInfo = new RecordsetInfo();
			RecordsetInfo.TypeIdentifier = dataSetBase.Name + "Recordset";
			RecordsetInfo.AutoptrTypeIdentifier = dataSetBase.Name + "RecordsetPtr";
			RecordsetInfo.FieldsInfos = FieldsInfos;
		}

		private void MegreFieldsWithLookups(ICollection<FieldInfo> fieldsInfos, ICollection<Lookup> lookups)
		{
			List<Lookup> lookupsRemains = new List<Lookup>(lookups);
			foreach (FieldInfo fi in fieldsInfos)
			{
				Int32 lookupIndex = lookupsRemains.FindIndex(delegate(Lookup lookup)
				{
					return lookup.Name == fi.FieldName;
				});
				if (lookupIndex != -1)
				{
					fi.Lookup = lookupsRemains[lookupIndex];
					CheckLookup(fi);
					lookupsRemains.RemoveAt(lookupIndex);
				}
			}
			foreach (Lookup lookup in lookupsRemains)
			{
				FieldInfo newFieldInfo;
				if (lookup.Type == OleDbType.Empty)
				{
					DataSetBase lookupDataset = Context.CurrentContext.FindQuery(lookup.DataSet);
					if (lookupDataset == null)
					{
						throw new InvalidInputDataException("Lookup dataset not found: " + lookup.DataSet);
					}
					FieldInfo fieldInfo = lookupDataset.Info.FindFieldByName(lookup.LookupResultField);
					if (fieldInfo == null)
					{
						throw new InvalidDataException("Результирующее Lookup поле '" + lookup.LookupResultField + "' не найдено в рекордсете '" + lookup.DataSet + "'");
					}
					newFieldInfo = fieldInfo.Clone();
				}
				else
				{
					newFieldInfo = new FieldInfo();
					newFieldInfo.FieldType = FieldTypeOleDBTypeMapper.Map(lookup.Type);
					newFieldInfo.Size = lookup.Size;
				}
				newFieldInfo.FieldName = lookup.Name;
				newFieldInfo.Lookup = lookup;
				CheckLookup(newFieldInfo);
				fieldsInfos.Add(newFieldInfo);
			}
		}

		protected void CheckLookup(FieldInfo fieldInfo)
		{
			if (fieldInfo.Lookup == null || fieldInfo.Lookup.DataSet == null)
			{
				return;
			}
			DataSetBase lookupDataset = Context.CurrentContext.FindQuery(fieldInfo.Lookup.DataSet);
			if (lookupDataset == null)
			{
				throw new InvalidInputDataException("Lookup dataset not found: " + fieldInfo.Lookup.DataSet);
			}
			if (lookupDataset.Info.FindFieldByName(fieldInfo.Lookup.LookupResultField) == null)
			{
				throw new InvalidDataException("Результирующее Lookup поле '" + fieldInfo.Lookup.LookupResultField + "' не найдено в рекордсете '" + fieldInfo.Lookup.DataSet + "'");
			}
			String[] localKeyFields = fieldInfo.Lookup.LocalKeyFields.Split(new Char[] {';'});
			String[] lookupKeyFields = fieldInfo.Lookup.LookupKeyFields.Split(new Char[] { ';' });
			if (localKeyFields.Length != lookupKeyFields.Length)
			{
				throw new InvalidDataException("Количество полей в lookupKeyFields не равно количеству полей в localKeyFields");
			}
			foreach (String localKeyField in localKeyFields)
			{
				// check existance of such field
			}
			foreach (String lookupKeyField in lookupKeyFields)
			{ 
				// check existance of such field
			}
		}

		protected void MegreFieldsWithCalcFields(ICollection<FieldInfo> fieldsInfos, ICollection<CalcField> calcFields)
		{
			List<CalcField> calcsRemains = new List<CalcField>(calcFields);
			foreach (FieldInfo fi in fieldsInfos)
			{
				Int32 calcIndex = calcsRemains.FindIndex(delegate(CalcField calcField)
				{
					return calcField.Name == fi.FieldName;
				});
				if (calcIndex != -1)
				{
					fi.CalcField = calcsRemains[calcIndex];
					calcsRemains.RemoveAt(calcIndex);
				}
			}
			foreach (CalcField calcField in calcsRemains)
			{
				OleDbFieldInfo oledbFi = new OleDbFieldInfo();
				oledbFi.FieldName = calcField.Name;
				oledbFi.FieldType = FieldTypeOleDBTypeMapper.Map(calcField.Type);
				FieldInfo fi = ExtractFieldInfo(null, oledbFi);
				fi.CalcField = calcField;
				fieldsInfos.Add(fi);
			}
		}

		private ICollection<FieldInfo> ExtractFieldsInfos(ICollection<OleDbFieldInfo> oleDbFieldsInfos, ICollection<Rename> renames)
		{
			List<FieldInfo> result = new List<FieldInfo>();
			foreach (OleDbFieldInfo oleDbFieldInfo in oleDbFieldsInfos)
			{
				result.Add(ExtractFieldInfo(renames, oleDbFieldInfo));
			}
			return result;
		}

		private FieldInfo ExtractFieldInfo(ICollection<Rename> renames, OleDbFieldInfo oleDbFieldInfo)
		{
			FieldInfo fieldInfo = new FieldInfo(oleDbFieldInfo);
			String renamedName = FindRename(oleDbFieldInfo.FieldName, renames);
			if (renamedName != null)
			{
				fieldInfo.RenamedName = renamedName;
			}
			return fieldInfo;
		}

		private String FindRename(String name, IEnumerable<Rename> renames)
		{
			if (renames != null)
			{
				foreach (Rename rename in renames)
				{
					if (rename.TargetName == name)
						return rename.NewName;
				}
			}
			return null;
		}
	}

	public class TableInfo : DataSetInfoBase
	{
		public String TableName;
	}

	public class Table : DataSetBase
	{
		[XmlAttribute("tableName")]
		public String TableName;

		private TableInfo _tableInfo;

		public TableInfo TableInfo
		{
			get
			{
				if (_tableInfo == null)
				{
					ParserInfo parserInfo = Context.CurrentContext.SqlParser.ParseSQL(TableName, CommandType.TableDirect, null);
					_tableInfo = new TableInfo();
					_tableInfo.Fill(this, parserInfo);
					if (Name == null)
					{
						_tableInfo.TypeIdentifier = TableName;
					}
					else
					{
						_tableInfo.TypeIdentifier = Name;
					}
					_tableInfo.TableName = TableName;
				}
				return _tableInfo;
			}
		}

		public override void Write(Generator generator)
		{
			generator.WriteTable(this.TableInfo);
		}

		public override DataSetInfoBase Info
		{
			get { return TableInfo; }
		}
	}

	public class Parameter
	{
		[XmlAttribute("name")]
		public String Name;
		[XmlAttribute("type")]
		public System.Data.OleDb.OleDbType Type;
		[XmlAttribute("size")]
		public Int32 Size;
	}

	public class Rename
	{
		[XmlAttribute("targetName")]
		public String TargetName;
		[XmlAttribute("newName")]
		public String NewName;
	}

	public class Lookup
	{
		[XmlAttribute("name")]
		public String Name;
		[XmlAttribute("localKeyFields")]
		public String LocalKeyFields;
		[XmlAttribute("lookupKeyFields")]
		public String RawLookupKeyFields;
		public String LookupKeyFields
		{
			get
			{
				if (RawLookupKeyFields == null)
				{
					return LocalKeyFields;
				}
				else
				{
					return RawLookupKeyFields;
				}
			}
		}
		[XmlAttribute("lookupResultField")]
		public String LookupResultField;
		[XmlAttribute("dataSet")]
		public String DataSet;
		[XmlAttribute("type")]
		public System.Data.OleDb.OleDbType Type;
		[XmlAttribute("size")]
		public Int32 Size;
	}

	public class DataSetInfo : DataSetInfoBase
	{
		public ICollection<Command> Commands;
	}

	public class DataSet : DataSetBase
	{
		[XmlElement("command")]
		public List<Command> Commands;

		private DataSetInfo _dataSetInfo;

		public DataSetInfo DataSetInfo
		{
			get
			{
				if (_dataSetInfo == null)
				{
					Command mainCommand = Commands[0];
					ParserInfo parserInfo;
					try
					{
                        if (mainCommand.StoredProc != null)
                        {
                            parserInfo = Context.CurrentContext.SqlParser.ParseSQL(mainCommand.StoredProc,
                                CommandType.StoredProcedure, mainCommand.Parameters);
                        }
                        else
                        {
                            parserInfo = Context.CurrentContext.SqlParser.ParseSQL(mainCommand.NormalizedSql,
                                CommandType.Text, mainCommand.Parameters);
                        }
                        // TODO: make tabledirect command type
					}
					catch (InvalidQueryException ex)
					{
						throw new InvalidQueryException(ex.Message, mainCommand.Name,
							mainCommand.Sql, ex.InnerException);
					}

					_dataSetInfo = new DataSetInfo();
					_dataSetInfo.Fill(this, parserInfo);
					_dataSetInfo.Commands = Commands;
				}
				return _dataSetInfo;
			}
		}

		public override void Write(Generator generator)
		{
			generator.WriteDataSet(this.DataSetInfo);
		}

		public override DataSetInfoBase Info
		{
			get { return DataSetInfo; }
		}
	}

	public class Command
	{
		[XmlAttribute("name")]
		public String Name;
		[XmlAttribute("methodName")]
		public String MethodName;
        [XmlAttribute("storedProc")]
        public String StoredProc;

		[XmlElement("parameter")]
		public List<Parameter> Parameters;

		[XmlElement("sql")]
		public String Sql;

		public String NormalizedSql
		{
			get
			{
				return Utils.ChopComments(Sql).Trim();
			}
		}
	}

	public class FieldInfo : OleDbFieldInfo, ICloneable
	{
		public Lookup Lookup;
		public CalcField CalcField;
		public String RenamedName;

		public FieldInfo() { }

		public FieldInfo(OleDbFieldInfo orig)
			: base(orig)
		{
		}

		public FieldInfo(FieldInfo orig)
			: base(orig)
		{
			Lookup = orig.Lookup;
			CalcField = orig.CalcField;
		}

		public FieldInfo Clone()
		{
			return (FieldInfo)((ICloneable)this).Clone();
		}

		#region ICloneable Members

		object ICloneable.Clone()
		{
			return new FieldInfo(this);
		}

		#endregion
	}

	public class QueryInfo : DataSetInfoBase
	{
		public String SqlText;
		public ICollection<Parameter> Parameters;
	}

	public class Query : DataSetBase
	{
        [XmlAttribute("storedProc")]
        public String StoredProc;
		[XmlElement("sql")]
		public String Sql;
		[XmlElement("parameter")]
		public List<Parameter> Parameters;

		public String NormalizedSql
		{
			get
			{
				return Utils.ChopComments(Sql).Trim();
			}
		}

		private QueryInfo _queryInfo = null;

		public QueryInfo QueryInfo
		{
			get
			{
				if (_queryInfo == null)
				{
					ParserInfo parserInfo;
					_queryInfo = new QueryInfo();
					try
					{
                        if (StoredProc != null)
                        {
                            parserInfo = Context.CurrentContext.SqlParser.ParseSQL(StoredProc,
                                CommandType.StoredProcedure, Parameters);
					        _queryInfo.SqlText = StoredProc;
                        }
                        else
                        {
                            parserInfo = Context.CurrentContext.SqlParser.ParseSQL(NormalizedSql,
                                CommandType.Text, Parameters);
					        _queryInfo.SqlText = NormalizedSql;
                        }
					}
					catch (InvalidQueryException ex)
					{
						throw new InvalidQueryException(ex.Message, Name, Sql, ex.InnerException);
					}
					_queryInfo.Fill(this, parserInfo);
					_queryInfo.Parameters = Parameters;
					_queryInfo.TypeIdentifier = Name;
				}
				return _queryInfo;
			}
		}

		public override void Write(Generator generator)
		{
			generator.WriteQuery(this.QueryInfo);
		}

		public override DataSetInfoBase Info
		{
			get { return QueryInfo; }
		}
	}

	public class CalcField
	{
		[XmlAttribute("name")]
		public String Name;
		[XmlAttribute("type")]
		public System.Data.OleDb.OleDbType Type;
	}

	public class Connection
	{
		[XmlElement("connectionString")]
		public String ConnectionString;
		[XmlAttribute("src")]
		public String ConnectionSource;
	}

	public class Global
	{
		[XmlElement("connection")]
		public String Connection;
	}

	[XmlRoot("safeQueryGen")]
	public class SafeQueryGenDocument
	{
		[XmlAttribute("reference")]
		public String[] References;

		[XmlElement("connection")]
		public Connection Connection;

		[XmlElement("global")]
		public Global Global;

		[XmlElement("query", typeof(Query)),
		XmlElement("dataSet", typeof(DataSet)),
		XmlElement("table", typeof(Table))]
		public DataSetBase[] Elements;

		private SafeQueryGenDocument[] _referencedDocs;

		public static SafeQueryGenDocument Deserialize(String fileName)
		{
			using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
			{
				XmlSerializer serializer = new XmlSerializer(typeof(SafeQueryGenDocument));
				SafeQueryGenDocument result = (SafeQueryGenDocument)serializer.Deserialize(stream);
				if (result.Connection.ConnectionSource != null)
				{
					// Текущая папка для udl-файла - папка в которой находится этот
					// xml-файл
					String connectionFilePath;
					String thisFileDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName));
					connectionFilePath = Path.Combine(thisFileDirectory, result.Connection.ConnectionSource);
					using (StreamReader connectionStream = new StreamReader(
						connectionFilePath, Encoding.ASCII))
					{
						// skip 2 first lines
						connectionStream.ReadLine();
						connectionStream.ReadLine();
						result.Connection.ConnectionString = connectionStream.ReadLine();
					}
				}
				return result;
			}
		}

		public SafeQueryGenDocument[] ResolveReferences()
		{
			List<SafeQueryGenDocument> result = new List<SafeQueryGenDocument>();
			if (References == null || References.Length == 0)
			{
				return result.ToArray();
			}
			foreach (String reference in References)
			{
				SafeQueryGenDocument referencedDoc = SafeQueryGenDocument.Deserialize(reference);
				result.Add(referencedDoc);
				result.AddRange(referencedDoc.ResolveReferences());
			}
			return result.ToArray();
		}

		public SafeQueryGenDocument[] ReferencedDocs
		{
			get
			{
				if (_referencedDocs == null)
				{
					_referencedDocs = ResolveReferences();
				}
				return _referencedDocs;
			}
		}
	}
}
