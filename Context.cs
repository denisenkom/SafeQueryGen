using System;
using System.Collections.Generic;
using System.Text;

namespace SafeQueryGen
{
	class Context
	{
		private Context()
		{
		}

		private static Context _currentContext;

		public static Context CurrentContext
		{
			get
			{
				if (_currentContext == null)
				{
					_currentContext = new Context();
				}
				return _currentContext;
			}
		}

		private OleDbSqlParser _sqlParser;
		private SafeQueryGenDocument _doc;

		public OleDbSqlParser SqlParser
		{
			get { return _sqlParser; }
			set { _sqlParser = value; }
		}

		public SafeQueryGenDocument Document
		{
			get { return _doc; }
			set { _doc = value; }
		}

		public DataSetBase FindQuery(String name)
		{
			foreach (DataSetBase dataSet in _doc.Elements)
			{
				if (dataSet.Name == name)
				{
					return dataSet;
				}
			}
			foreach(SafeQueryGenDocument doc in _doc.ReferencedDocs)
			{
				foreach (DataSetBase dataSet in doc.Elements)
				{
					if (dataSet.Name == name)
					{
						return dataSet;
					}
				}
			}
			return null;
		}
	}
}
