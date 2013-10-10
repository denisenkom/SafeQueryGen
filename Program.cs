using System;
using System.IO;
using System.Data.OleDb;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace SafeQueryGen
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("SafeQueryGen usage: inFile [outFile]");
				Environment.Exit(1);
			}
			String inFile = args[0];
			String outFile;
			if (args.Length < 2)
			{
				outFile = inFile.Substring(0, inFile.LastIndexOf('.')) + ".h";
			}
			else
			{
				outFile = args[1];
			}
			String inFileDirectory = Path.GetDirectoryName(Path.GetFullPath(inFile));
			String outFileDirectory = Path.GetDirectoryName(Path.GetFullPath(outFile));
			inFile = Path.Combine(inFileDirectory, Path.GetFileName(inFile));
			outFile = Path.Combine(outFileDirectory, Path.GetFileName(outFile));
			Environment.CurrentDirectory = inFileDirectory;
			Console.WriteLine(inFile + ":");
			try
			{
				SafeQueryGenDocument document = SafeQueryGenDocument.Deserialize(inFile);
				Generator generator = new Generator();
				generator.Generate(inFile, outFile);
			}
			catch (FileNotFoundException ex)
			{
				Console.WriteLine(ex.Message);
				Environment.Exit(2);
			}
			catch (XmlSchemaValidationException ex)
			{
				Console.WriteLine("Error: {0}({1}): {2}", inFile, ex.LineNumber, ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Environment.Exit(2);
			}
			catch (XmlException ex)
			{
				Console.WriteLine("Error: {0}({1}): {2}", inFile, ex.LineNumber, ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Environment.Exit(2);
			}
			catch (CannotOpenOutputFileException ex)
			{
				Console.WriteLine("Error: {0}", ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Environment.Exit(2);
			}
			catch (InvalidXmlFormat ex)
			{
				Console.WriteLine("Error: Неправильный формат входного файла: {0}", ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Environment.Exit(2);
			}
			catch (InvalidQueryException ex)
			{
				Console.WriteLine("Error: Invalid query {0} {1}", ex.Message, ex.QueryName);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Console.WriteLine(ex.Sql);
				Environment.Exit(2);
			}
			catch (CannotConnectToDBException ex)
			{
				Console.WriteLine("Error: {0}", ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine(ex.InnerException.Message);
				}
				Console.WriteLine(ex.ConnectionString);
				Environment.Exit(2);
			}
		}
	}
}
