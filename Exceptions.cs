using System;

namespace SafeQueryGen
{
	class InvalidInputDataException : Exception
	{
		public InvalidInputDataException(String message)
			: base(message)
		{
		}

		public InvalidInputDataException(String message, Exception innerException)
			: base(message, innerException)
		{
		}
	}

	class InvalidXmlFormat : InvalidInputDataException
	{
		public InvalidXmlFormat(String message)
			: base(message)
		{
		}

		public InvalidXmlFormat(String message, Exception innerException)
			: base(message, innerException)
		{
		}
	}

	class InvalidQueryException : InvalidInputDataException
	{
		private String _queryName;
		private String _sql;

		public InvalidQueryException(String message, String sql, Exception innerException)
			: base(message, innerException)
		{
			_sql = sql;
		}

		public InvalidQueryException(String message, String name, String sql)
			: base(message)
		{
			_queryName = name;
			_sql = sql;
		}

		public InvalidQueryException(String message, String name, String sql, Exception innerException)
			: base(message, innerException)
		{
			_queryName = name;
			_sql = sql;
		}

		public String QueryName { get { return _queryName; } }
		public String Sql { get { return _sql; } }
	}

	class CannotConnectToDBException : InvalidInputDataException
	{
		private String _connectionString;

		public CannotConnectToDBException(String message, String connectionString)
			: base(message)
		{
			_connectionString = connectionString;
		}

		public CannotConnectToDBException(String message, String connectionString, Exception innerException)
			: base(message, innerException)
		{
			_connectionString = connectionString;
		}

		public String ConnectionString { get { return _connectionString; } }
	}

	class CannotOpenOutputFileException : InvalidInputDataException
	{
		public CannotOpenOutputFileException(String message)
			: base(message)
		{
		}

		public CannotOpenOutputFileException(String message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}