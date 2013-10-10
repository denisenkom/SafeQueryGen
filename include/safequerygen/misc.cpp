#include "misc.h"
#include <vcl\comobj.hpp>
#include <list>

namespace SafeQueryGen
{

	extern const int LockTypeValues[] = {-1/*adLockUnspecified*/,
		1/*adLockReadOnly*/, 2/*adLockPessimistic*/, 3/*adLockOptimistic*/,
		4/*adLockBatchOptimistic*/};

	extern const int CursorLocationValues[] = {2 /*adUseServer*/, 3 /*adUseClient*/};

	extern const int CursorTypeValues[] = {-1/*adOpenUnspecified*/,
		0/*adOpenForwardOnly*/, 1/*adOpenKeyset*/, 2/*adOpenDynamic*/, 3/*adOpenStatic*/};

	extern const int CommandTypeValues[] = {8/*adCmdUnknown*/,
		1/*adCmdText*/, 2/*adCmdTable*/, 4/*adCmdStoredProc*/, 256/*adCmdFile*/, 512/*adCmdTableDirect*/};

	extern const int ExecuteOptionValues[] = {0x10/*adAsyncExecute*/,
		0x20/*adAsyncFetch*/, 0x40/*adAsyncFetchNonBlocking*/, 0x80/*adExecuteNoRecords*/};

	void ThrowOleException(HRESULT hres)
	{
		WideString source, description, helpFile;
		DWORD helpContext = 0;
		IErrorInfo * errorInfo = 0;
		if (::GetErrorInfo(0, &errorInfo) == S_OK)
		{
			errorInfo->GetSource(&source);
			errorInfo->GetDescription(&description);
			errorInfo->GetHelpFile(&helpFile);
			errorInfo->GetHelpContext(&helpContext);
			errorInfo->Release();
		}
		throw EOleException(description, hres, source, helpFile, helpContext);
	}

	int ExecuteOptionsToOrd(TExecuteOptions executeOptions)
	{
		int result = 0;
		TExecuteOption values[] = {eoAsyncExecute, eoAsyncFetch,
			eoAsyncFetchNonBlocking, eoExecuteNoRecords};
		if (executeOptions != TExecuteOptions())
		{
			for (int i = 0; i < sizeof(values)/sizeof(values[0]); i++)
			{
				if (executeOptions.Contains(values[i]))
				{
					result |= ExecuteOptionValues[values[i]];
				}
			}
		}
		return result;
	}

	void OpenDatasetWithCommand(Adodb::TCustomADODataSet * dataset, Adodb::TADOCommand * command)
	{
		// заполняем свойства ADODB объектов данными из VCL объектов
        OleCheckException(command->CommandObject->Set_ActiveConnection(command->Connection->ConnectionObject));
		if (command->Parameters->Count > 0)
		{
			int paramsCount;

			// некоторые команды могут возвращать ошибку если вызвать Parameters.Count, и если при
			// этом параметры отсутствуют
			try
			{
				paramsCount = command->CommandObject->Parameters->Count;
			}
			catch (EOleException & ex)
			{
				paramsCount = 0;
			}
			while (paramsCount > 0)
			{
				command->CommandObject->Parameters->Delete(0);
				paramsCount--;
			}
			for (int i = 0; i < command->Parameters->Count; i++)
			{
				OleCheckException(command->CommandObject->Parameters->Append(command->Parameters->Items[i]->ParameterObject));
			}
		}
		Adoint::_di__Recordset rs = Adoint::CoRecordset::Create(0);
        rs->CursorLocation = CursorLocationValues[dataset->CursorLocation];
		OleCheckException(rs->Open((IUnknown*)command->CommandObject,
			System::Variant::NoParam(),
			CursorTypeValues[dataset->CursorType],
			LockTypeValues[dataset->LockType],
			CommandTypeValues[command->CommandType] | ExecuteOptionsToOrd(command->ExecuteOptions)));
        dataset->Recordset = rs;
	}

	TADOConnection * _defaultConnection = 0;

	TADOConnection * GetDefaultConnection()
	{
		return _defaultConnection;
	}

	void SetDefaultConnection(TADOConnection * connection)
	{
		_defaultConnection = connection;
	}


	static std::list<Adodb::TCustomADODataSet **> _lookups;

	void _RegisterLookup(Adodb::TCustomADODataSet ** ptrptr)
	{
		_lookups.push_back(ptrptr);
	}


	void FreeAllLookups()
	{
		std::list<Adodb::TCustomADODataSet **>::iterator i;
		for (i = _lookups.begin(); i != _lookups.end(); i++)
		{
			delete *(*i);
			*(*i) = 0;
		}
		_lookups.clear();
	}

} // namespace SafeQueryGen
